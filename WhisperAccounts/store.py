"""Server-side entitlements. No client-provided paid flag or local Pro bypass."""
from contextlib import contextmanager
import os
import hashlib
import hmac
import json
from pathlib import Path
import sqlite3
import time

from .identity import AccountError, VerifiedIdentity, canonical_email

OWNER_EMAIL = canonical_email(os.environ.get('WHISPER_OWNER_EMAIL', 'owner@example.com'))
FOUNDERS = tuple(dict.fromkeys((OWNER_EMAIL, *(canonical_email(value.strip()) for value in os.environ.get('WHISPER_FOUNDER_EMAILS', 'cofounder@example.com').split(',') if value.strip()))))


class AccountStore:
    def __init__(self, path: str, owner_subject: str, audit_key: bytes):
        if len(audit_key) < 32:
            raise ValueError('A secret audit key of at least 32 bytes is required.')
        self.path, self.owner_subject, self.audit_key = path, owner_subject, audit_key
        Path(path).parent.mkdir(parents=True, exist_ok=True)
        with self.connection() as db:
            db.executescript('''
                CREATE TABLE IF NOT EXISTS accounts(
                    subject TEXT PRIMARY KEY, email TEXT NOT NULL, authoritative INTEGER NOT NULL,
                    suspended INTEGER NOT NULL DEFAULT 0, paid_until INTEGER);
                CREATE TABLE IF NOT EXISTS grants(
                    email TEXT PRIMARY KEY, subject TEXT REFERENCES accounts(subject),
                    kind TEXT NOT NULL, active INTEGER NOT NULL DEFAULT 1, created_at INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS audit(
                    id INTEGER PRIMARY KEY, payload TEXT NOT NULL, mac TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS sessions(
                    token_hash TEXT PRIMARY KEY, subject TEXT NOT NULL REFERENCES accounts(subject),
                    csrf_hash TEXT NOT NULL, expires INTEGER NOT NULL, admin INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS challenges(
                    token_hash TEXT PRIMARY KEY, nonce TEXT NOT NULL, csrf_hash TEXT NOT NULL,
                    expires INTEGER NOT NULL);
            ''')
            for email in FOUNDERS:
                changed = db.execute('INSERT OR IGNORE INTO grants(email,kind,created_at) VALUES(?,?,?)',
                                     (email, 'founder_lifetime', int(time.time()))).rowcount
                if changed:
                    self.audit(db, 'seed', 'deployment', email)

    @contextmanager
    def connection(self):
        db = sqlite3.connect(self.path, timeout=10)
        db.row_factory = sqlite3.Row
        db.execute('PRAGMA foreign_keys=ON')
        try:
            with db:
                yield db
        finally:
            db.close()

    def fingerprint(self, value: str) -> str:
        return hmac.new(self.audit_key, value.encode(), hashlib.sha256).hexdigest()

    def audit(self, db, action: str, actor: str, target: str):
        previous = db.execute('SELECT mac FROM audit ORDER BY id DESC LIMIT 1').fetchone()
        payload = json.dumps({'action': action, 'actor': self.fingerprint(actor),
                              'target': self.fingerprint(target), 'time': int(time.time()),
                              'previous': previous['mac'] if previous else ''}, sort_keys=True)
        mac = hmac.new(self.audit_key, payload.encode(), hashlib.sha256).hexdigest()
        db.execute('INSERT INTO audit(payload,mac) VALUES(?,?)', (payload, mac))

    def is_owner(self, identity: VerifiedIdentity) -> bool:
        return bool(self.owner_subject and identity.subject == self.owner_subject and
                    identity.email == OWNER_EMAIL and identity.authoritative_email)

    def require_owner(self, identity: VerifiedIdentity):
        if not self.is_owner(identity):
            raise AccountError('FORBIDDEN', 'Owner access is required.', 403)

    def register(self, identity: VerifiedIdentity):
        with self.connection() as db:
            db.execute('INSERT INTO accounts(subject,email,authoritative) VALUES(?,?,?) '
                       'ON CONFLICT(subject) DO UPDATE SET email=excluded.email, authoritative=excluded.authoritative',
                       (identity.subject, identity.email, int(identity.authoritative_email)))
            # Pending email grants attach once. Later email changes preserve subject ownership.
            if identity.authoritative_email:
                changed = db.execute('UPDATE grants SET subject=? WHERE email=? AND subject IS NULL',
                                     (identity.subject, identity.email)).rowcount
                if changed:
                    self.audit(db, 'attach', identity.subject, identity.email)

    def grant(self, actor: VerifiedIdentity, email: str):
        self.require_owner(actor)
        email = canonical_email(email)
        with self.connection() as db:
            accounts = db.execute('SELECT subject,authoritative FROM accounts WHERE email=?', (email,)).fetchall()
            if len(accounts) > 1:
                raise AccountError('AMBIGUOUS_ACCOUNT', 'Multiple accounts match; subject-based recovery is required.')
            target = accounts[0] if accounts else None
            if not email.endswith('@gmail.com') and not (target and target['authoritative']):
                raise AccountError('VERIFY_ACCOUNT_FIRST', 'Non-Gmail accounts must sign in with a verified Workspace identity first.')
            existing = db.execute('SELECT * FROM grants WHERE email=?', (email,)).fetchone()
            if existing and existing['kind'] == 'founder_lifetime':
                return
            if existing and existing['subject'] and target and existing['subject'] != target['subject']:
                raise AccountError('ACCOUNT_BINDING_CONFLICT', 'A grant already belongs to another Google identity.')
            subject = existing['subject'] if existing and existing['subject'] else (target['subject'] if target else None)
            db.execute('INSERT INTO grants(email,subject,kind,created_at) VALUES(?,?,?,?) '
                       'ON CONFLICT(email) DO UPDATE SET active=1, subject=COALESCE(grants.subject,excluded.subject)',
                       (email, subject, 'owner_complimentary', int(time.time())))
            self.audit(db, 'grant', actor.subject, email)

    def revoke(self, actor: VerifiedIdentity, email: str):
        self.require_owner(actor)
        email = canonical_email(email)
        with self.connection() as db:
            grant = db.execute('SELECT kind FROM grants WHERE email=?', (email,)).fetchone()
            if not grant:
                raise AccountError('NOT_FOUND', 'Grant not found.', 404)
            if grant['kind'] == 'founder_lifetime':
                raise AccountError('LIFETIME_PROTECTED', 'The two promised lifetime grants cannot be removed here.', 409)
            db.execute('UPDATE grants SET active=0 WHERE email=?', (email,))
            self.audit(db, 'revoke', actor.subject, email)

    def list_grants(self, actor: VerifiedIdentity) -> list[dict]:
        self.require_owner(actor)
        with self.connection() as db:
            return [dict(row) for row in db.execute('SELECT email,kind,active,subject,created_at FROM grants ORDER BY email')]

    def entitlement(self, subject: str) -> dict:
        with self.connection() as db:
            account = db.execute('SELECT * FROM accounts WHERE subject=?', (subject,)).fetchone()
            if not account or account['suspended']:
                return {'access': False, 'status': 'suspended' if account else 'login_required'}
            grant = db.execute('SELECT kind FROM grants WHERE subject=? AND active=1 ORDER BY kind LIMIT 1', (subject,)).fetchone()
            if grant:
                return {'access': True, 'status': grant['kind'], 'tier': 'all_features',
                        'payment_required': False, 'expires_at': None, 'product_usage_limit': None}
            if account['paid_until'] and account['paid_until'] > time.time():
                return {'access': True, 'status': 'paid', 'tier': 'all_features',
                        'payment_required': False, 'expires_at': account['paid_until']}
            return {'access': False, 'status': 'payment_required', 'payment_required': True}

    def verify_audit(self) -> bool:
        previous = ''
        with self.connection() as db:
            for row in db.execute('SELECT payload,mac FROM audit ORDER BY id'):
                expected = hmac.new(self.audit_key, row['payload'].encode(), hashlib.sha256).hexdigest()
                if not hmac.compare_digest(expected, row['mac']) or json.loads(row['payload'])['previous'] != previous:
                    return False
                previous = row['mac']
        return True
