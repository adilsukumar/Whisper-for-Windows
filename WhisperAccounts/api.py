"""Owner console API. Hosting, TLS, OAuth configuration and billing are deployment prerequisites."""
from collections import defaultdict, deque
from dataclasses import dataclass
import hashlib
import hmac
import os
from pathlib import Path
import secrets
import threading
import time
from urllib.parse import urlsplit

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import FileResponse, JSONResponse
from pydantic import BaseModel, ConfigDict, Field
from starlette.middleware.trustedhost import TrustedHostMiddleware

from .identity import AccountError, GoogleVerifier, VerifiedIdentity
from .store import AccountStore
from .desktop import install_desktop_routes


def digest(value: str) -> str:
    return hashlib.sha256(value.encode()).hexdigest()


@dataclass(frozen=True)
class Config:
    origin: str
    client_id: str
    owner_subject: str
    database: str
    audit_key: bytes
    dev_http: bool = False

    def validate(self):
        url = urlsplit(self.origin)
        if not url.hostname or url.path or url.query or url.fragment or url.username or url.password:
            raise ValueError('Configure one exact public origin without a path or credentials.')
        if url.scheme != 'https' and not (self.dev_http and url.scheme == 'http' and url.hostname in {'localhost', '127.0.0.1'}):
            raise ValueError('HTTPS is required except explicit loopback development.')
        if not self.owner_subject:
            raise ValueError('Pin the verified owner Google subject before enabling administration.')


class LoginBody(BaseModel):
    model_config = ConfigDict(extra='forbid')
    credential: str = Field(min_length=1, max_length=16384)


class GrantBody(BaseModel):
    model_config = ConfigDict(extra='forbid')
    email: str = Field(min_length=3, max_length=254)


class RateGate:
    def __init__(self):
        self.entries = defaultdict(deque)
        self.lock = threading.Lock()

    def check(self, key: str, limit: int):
        now = time.monotonic()
        with self.lock:
            # Single-process development limiter; production needs a shared edge limiter.
            if len(self.entries) > 10000:
                for old in list(self.entries):
                    if not self.entries[old] or self.entries[old][-1] < now - 60:
                        self.entries.pop(old, None)
                if len(self.entries) > 10000:
                    raise AccountError('RATE_LIMITED', 'Try again later.', 429)
            hits = self.entries[key]
            while hits and hits[0] < now - 60:
                hits.popleft()
            if len(hits) >= limit:
                raise AccountError('RATE_LIMITED', 'Try again later.', 429)
            hits.append(now)


def create_app(config: Config, *, verifier=None):
    config.validate()
    verifier = verifier or GoogleVerifier(config.client_id)
    store = AccountStore(config.database, config.owner_subject, config.audit_key)
    app = FastAPI(docs_url=None, redoc_url=None, openapi_url=None)
    app.state.store = store
    app.add_middleware(TrustedHostMiddleware, allowed_hosts=[urlsplit(config.origin).hostname])
    secure = urlsplit(config.origin).scheme == 'https'
    session_cookie = '__Host-whisper_session' if secure else 'whisper_session_dev'
    csrf_cookie = '__Host-whisper_csrf' if secure else 'whisper_csrf_dev'
    challenge_cookie = '__Host-whisper_login' if secure else 'whisper_login_dev'
    gate = RateGate()
    assets = Path(__file__).parent / 'web'

    def csrf(request: Request, expected_hash: str):
        supplied = request.headers.get('X-CSRF-Token', '')
        cookie = request.cookies.get(csrf_cookie, '')
        if (request.headers.get('Origin') != config.origin or not supplied or not cookie or
            not hmac.compare_digest(supplied, cookie) or not hmac.compare_digest(digest(supplied), expected_hash)):
            raise AccountError('CSRF_REJECTED', 'Request origin or security token is invalid.', 403)

    def session(request: Request, mutation=False, admin=False):
        token = request.cookies.get(session_cookie, '')
        with store.connection() as db:
            row = db.execute('SELECT sessions.*,accounts.email,accounts.authoritative,accounts.suspended '
                             'FROM sessions JOIN accounts ON accounts.subject=sessions.subject WHERE token_hash=?',
                             (digest(token),)).fetchone()
        if not token or not row or row['expires'] <= time.time() or row['suspended']:
            raise AccountError('LOGIN_REQUIRED', 'Sign in again.', 401)
        identity = VerifiedIdentity(row['subject'], row['email'], bool(row['authoritative']), 0)
        if admin and (not row['admin'] or not store.is_owner(identity)):
            raise AccountError('FORBIDDEN', 'Owner access is required.', 403)
        if mutation:
            csrf(request, row['csrf_hash'])
        return identity

    def set_cookie(response, name, value, age, http_only=True):
        response.set_cookie(name, value, max_age=age, httponly=http_only, secure=secure, samesite='strict', path='/')

    @app.exception_handler(AccountError)
    async def account_error(request, error):
        return JSONResponse({'error': error.code, 'message': error.message}, status_code=error.status)

    @app.exception_handler(RequestValidationError)
    async def invalid_body(request, error):
        # FastAPI's default response can echo the submitted credential. Never do that.
        return JSONResponse({'error': 'INVALID_REQUEST', 'message': 'Invalid request body.'}, status_code=400)

    @app.middleware('http')
    async def hardening(request: Request, call_next):
        if secure and request.url.scheme != 'https':
            return JSONResponse({'error': 'HTTPS_REQUIRED'}, status_code=400)
        if request.method == 'POST':
            length = request.headers.get('content-length', '')
            if not length.isdigit() or int(length) > 20000:
                return JSONResponse({'error': 'BODY_LIMIT'}, status_code=413)
        response = await call_next(request)
        response.headers['Cache-Control'] = 'no-store'
        response.headers['X-Content-Type-Options'] = 'nosniff'
        response.headers['Referrer-Policy'] = 'no-referrer'
        response.headers['Content-Security-Policy'] = (
            "default-src 'self'; script-src 'self' https://accounts.google.com; "
            "connect-src 'self' https://accounts.google.com; frame-src https://accounts.google.com; "
            "style-src 'self' https://accounts.google.com; object-src 'none'; base-uri 'none'; "
            "frame-ancestors 'none'; form-action 'self'")
        if secure:
            response.headers['Strict-Transport-Security'] = 'max-age=31536000'
        return response

    @app.get('/admin')
    def admin_page():
        return FileResponse(assets / 'admin.html')

    @app.get('/admin.js')
    def admin_script():
        return FileResponse(assets / 'admin.js', media_type='application/javascript')

    @app.get('/admin.css')
    def admin_style():
        return FileResponse(assets / 'admin.css', media_type='text/css')

    @app.get('/auth/challenge')
    def challenge(request: Request):
        gate.check('challenge:' + request.client.host, 20)
        token, nonce, csrf_token = secrets.token_urlsafe(32), secrets.token_urlsafe(32), secrets.token_urlsafe(32)
        with store.connection() as db:
            db.execute('DELETE FROM challenges WHERE expires < ?', (int(time.time()),))
            db.execute('INSERT INTO challenges VALUES(?,?,?,?)', (digest(token), nonce, digest(csrf_token), int(time.time()) + 300))
        response = JSONResponse({'client_id': config.client_id, 'nonce': nonce})
        set_cookie(response, challenge_cookie, token, 300)
        set_cookie(response, csrf_cookie, csrf_token, 300, False)
        return response

    @app.post('/auth/google')
    def login(body: LoginBody, request: Request):
        gate.check('login:' + request.client.host, 10)
        token = request.cookies.get(challenge_cookie, '')
        with store.connection() as db:
            db.execute('BEGIN IMMEDIATE')
            pending = db.execute('SELECT * FROM challenges WHERE token_hash=?', (digest(token),)).fetchone()
            if not pending or pending['expires'] <= time.time():
                raise AccountError('LOGIN_EXPIRED', 'Start Google sign-in again.', 401)
            csrf(request, pending['csrf_hash'])
            # Consume atomically before external verification; failures require a new challenge.
            db.execute('DELETE FROM challenges WHERE token_hash=?', (digest(token),))
        identity = verifier.verify(body.credential, pending['nonce'])
        store.register(identity)
        admin = store.is_owner(identity)
        if admin and (identity.issued_at < time.time() - 300 or identity.issued_at > time.time() + 30):
            raise AccountError('FRESH_LOGIN_REQUIRED', 'Owner access requires a fresh Google sign-in.', 401)
        with store.connection() as db:
            account = db.execute('SELECT suspended FROM accounts WHERE subject=?', (identity.subject,)).fetchone()
            if account['suspended']:
                raise AccountError('ACCOUNT_SUSPENDED', 'This account is suspended.', 403)
            session_token, csrf_token = secrets.token_urlsafe(32), secrets.token_urlsafe(32)
            age = 900 if admin else 3600
            db.execute('DELETE FROM sessions WHERE expires < ?', (int(time.time()),))
            db.execute('INSERT INTO sessions VALUES(?,?,?,?,?)',
                       (digest(session_token), identity.subject, digest(csrf_token), int(time.time()) + age, int(admin)))
            if admin:
                store.audit(db, 'owner_login', identity.subject, identity.subject)
        response = JSONResponse({'is_owner': admin, 'entitlement': store.entitlement(identity.subject)})
        set_cookie(response, session_cookie, session_token, age)
        set_cookie(response, csrf_cookie, csrf_token, age, False)
        response.delete_cookie(challenge_cookie, path='/', secure=secure, samesite='strict')
        return response

    @app.get('/api/me')
    def me(request: Request):
        identity = session(request)
        return {'email': identity.email, 'google_subject': identity.subject,
                'is_owner': store.is_owner(identity), 'entitlement': store.entitlement(identity.subject)}

    @app.get('/api/grants')
    def grants(request: Request):
        return {'grants': store.list_grants(session(request, admin=True))}

    @app.get('/api/audit/status')
    def audit_status(request: Request):
        session(request, admin=True)
        return {'chain_valid': store.verify_audit()}

    @app.post('/api/grants')
    def grant(body: GrantBody, request: Request):
        actor = session(request, mutation=True, admin=True)
        gate.check('grant:' + actor.subject, 30)
        store.grant(actor, body.email)
        return {'ok': True}

    @app.post('/api/grants/revoke')
    def revoke(body: GrantBody, request: Request):
        store.revoke(session(request, mutation=True, admin=True), body.email)
        return {'ok': True}

    @app.post('/auth/logout')
    def logout(request: Request):
        identity = session(request, mutation=True)
        with store.connection() as db:
            db.execute('DELETE FROM sessions WHERE token_hash=?', (digest(request.cookies[session_cookie]),))
        response = JSONResponse({'ok': True})
        for name in (session_cookie, csrf_cookie, challenge_cookie):
            response.delete_cookie(name, path='/', secure=secure, samesite='strict')
        return response

    install_desktop_routes(app, store, config, session, gate)
    return app


def from_environment():
    return create_app(Config(
        origin=os.environ['WHISPER_ACCOUNTS_ORIGIN'], client_id=os.environ['WHISPER_GOOGLE_CLIENT_ID'],
        owner_subject=os.environ['WHISPER_OWNER_GOOGLE_SUB'], database=os.environ['WHISPER_ACCOUNTS_DB'],
        audit_key=os.environ['WHISPER_AUDIT_HMAC_KEY'].encode(),
        dev_http=os.environ.get('WHISPER_ACCOUNTS_DEV_HTTP') == '1'))
