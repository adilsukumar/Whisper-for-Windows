"""Browser-approved, PKCE-bound desktop sessions. Tokens are stored only as hashes."""
import base64
import hashlib
import hmac
import secrets
import time

from pydantic import BaseModel, ConfigDict, Field
from fastapi import Request
from .identity import AccountError, VerifiedIdentity


def hashed(value):
    return hashlib.sha256(value.encode()).hexdigest()


class StartBody(BaseModel):
    model_config = ConfigDict(extra='forbid')
    challenge: str = Field(pattern=r'^[A-Za-z0-9_-]{43}$')


class RedeemBody(BaseModel):
    model_config = ConfigDict(extra='forbid')
    request_id: str = Field(min_length=32, max_length=128)
    verifier: str = Field(pattern=r'^[A-Za-z0-9_-]{43,128}$')


class ApproveBody(BaseModel):
    model_config = ConfigDict(extra='forbid')
    request_id: str = Field(min_length=32, max_length=128)
    display_code: str = Field(pattern=r'^[A-F0-9]{8}$')


def install_desktop_routes(app, store, config, browser_session, gate):
    with store.connection() as db:
        db.executescript('''
            CREATE TABLE IF NOT EXISTS desktop_requests(
                request_hash TEXT PRIMARY KEY, challenge TEXT NOT NULL, display_code TEXT NOT NULL,
                expires INTEGER NOT NULL, subject TEXT);
            CREATE TABLE IF NOT EXISTS desktop_tokens(
                token_hash TEXT PRIMARY KEY, subject TEXT NOT NULL, expires INTEGER NOT NULL);
        ''')

    def desktop_identity(request):
        authorization = request.headers.get('Authorization', '')
        if not authorization.startswith('Bearer ') or len(authorization) > 256:
            raise AccountError('UNAUTHORIZED', 'Sign in to the desktop app again.', 401)
        with store.connection() as db:
            row = db.execute('SELECT accounts.*,desktop_tokens.expires FROM desktop_tokens '
                'JOIN accounts ON accounts.subject=desktop_tokens.subject WHERE token_hash=?',
                (hashed(authorization[7:]),)).fetchone()
        if not row or row['expires'] <= int(time.time()) or row['suspended']:
            raise AccountError('UNAUTHORIZED', 'Desktop session is expired or suspended.', 401)
        return VerifiedIdentity(row['subject'], row['email'], bool(row['authoritative']), 0)

    @app.post('/desktop/start')
    def start(body: StartBody, request: Request):
        gate.check('desktop-start:' + (request.client.host if request.client else 'unknown'), 10)
        request_id, display_code = secrets.token_urlsafe(32), secrets.token_hex(4).upper()
        with store.connection() as db:
            db.execute('DELETE FROM desktop_requests WHERE expires <= ?', (int(time.time()),))
            db.execute('INSERT INTO desktop_requests VALUES(?,?,?,?,NULL)',
                (hashed(request_id), body.challenge, display_code, int(time.time()) + 300))
        return {'request_id': request_id, 'display_code': display_code, 'expires_in': 300,
                'approval_url': config.origin + '/admin?desktop=' + request_id}

    @app.post('/api/desktop/approve')
    def approve(body: ApproveBody, request: Request):
        identity = browser_session(request, mutation=True)
        if not store.entitlement(identity.subject)['access']:
            raise AccountError('PAYMENT_REQUIRED', 'An active paid or complimentary entitlement is required.', 403)
        with store.connection() as db:
            db.execute('BEGIN IMMEDIATE')
            row = db.execute('SELECT * FROM desktop_requests WHERE request_hash=?', (hashed(body.request_id),)).fetchone()
            if not row or row['expires'] <= int(time.time()) or row['subject'] or not hmac.compare_digest(row['display_code'], body.display_code):
                raise AccountError('INVALID_APPROVAL', 'Approval is expired, already used, or does not match the desktop code.', 400)
            db.execute('UPDATE desktop_requests SET subject=? WHERE request_hash=?', (identity.subject, hashed(body.request_id)))
            store.audit(db, 'desktop-approved', identity.subject, identity.email)
        return {'ok': True}

    @app.post('/desktop/token')
    def redeem(body: RedeemBody, request: Request):
        gate.check('desktop-poll:' + hashed(body.request_id), 30)
        expected = base64.urlsafe_b64encode(hashlib.sha256(body.verifier.encode()).digest()).decode().rstrip('=')
        with store.connection() as db:
            db.execute('BEGIN IMMEDIATE')
            row = db.execute('SELECT * FROM desktop_requests WHERE request_hash=?', (hashed(body.request_id),)).fetchone()
            if not row or row['expires'] <= int(time.time()) or not hmac.compare_digest(row['challenge'], expected):
                raise AccountError('INVALID_DESKTOP_REQUEST', 'Desktop request could not be verified.', 400)
            if not row['subject']:
                return {'status': 'pending'}
            if not store.entitlement(row['subject'])['access']:
                raise AccountError('PAYMENT_REQUIRED', 'Account access is no longer active.', 403)
            # Atomic consume prevents parallel redemption/replay.
            removed = db.execute('DELETE FROM desktop_requests WHERE request_hash=?', (hashed(body.request_id),)).rowcount
            if removed != 1:
                raise AccountError('INVALID_DESKTOP_REQUEST', 'Desktop request was already used.', 400)
            token = secrets.token_urlsafe(32)
            db.execute('DELETE FROM desktop_tokens WHERE expires <= ?', (int(time.time()),))
            db.execute('INSERT INTO desktop_tokens VALUES(?,?,?)', (hashed(token), row['subject'], int(time.time()) + 3600))
        return {'status': 'approved', 'access_token': token, 'expires_in': 3600}

    @app.get('/desktop/me')
    def me(request: Request):
        identity = desktop_identity(request)
        return {'email': identity.email, 'entitlement': store.entitlement(identity.subject)}

    @app.post('/desktop/logout')
    def logout(request: Request):
        desktop_identity(request)
        with store.connection() as db:
            db.execute('DELETE FROM desktop_tokens WHERE token_hash=?', (hashed(request.headers['Authorization'][7:]),))
        return {'ok': True}
