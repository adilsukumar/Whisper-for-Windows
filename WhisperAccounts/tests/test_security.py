import json
from pathlib import Path
import tempfile
import time
import unittest
from unittest.mock import patch

from fastapi.testclient import TestClient
from WhisperAccounts.api import Config, create_app
from WhisperAccounts.identity import AccountError, GoogleVerifier, VerifiedIdentity, canonical_email
from WhisperAccounts.store import AccountStore, OWNER_EMAIL

CLIENT = 'real-client.apps.googleusercontent.com'

def identity(subject='owner-sub', email=OWNER_EMAIL, authoritative=True, age=0):
    return VerifiedIdentity(subject, email, authoritative, int(time.time()) - age)

class FakeVerifier:
    """Test injection only. No deployment flag enables fake verification."""
    def verify(self, token, nonce):
        users = {'owner': identity(), 'ordinary': identity('ordinary', 'ordinary@gmail.com'),
                 'wrong-owner': identity('wrong-sub'), 'stale': identity(age=600)}
        if token not in users:
            raise AccountError('INVALID_LOGIN', 'Google login could not be verified.', 401)
        return users[token]

class IdentityTests(unittest.TestCase):
    def claims(self):
        return {'iss': 'https://accounts.google.com', 'aud': CLIENT, 'sub': 'subject',
                'email': 'USER@gmail.com', 'email_verified': True, 'exp': int(time.time())+300,
                'iat': int(time.time()), 'nonce': 'nonce'}

    def test_verified_claims(self):
        result = GoogleVerifier(CLIENT).identity_from_verified_claims(self.claims(), 'nonce')
        self.assertEqual(result.email, 'user@gmail.com')
        self.assertTrue(result.authoritative_email)

    def test_invalid_claims(self):
        for field, value in [('aud','evil'),('iss','evil'),('email_verified',False),
                             ('email_verified','true'),('exp',1),('iat',int(time.time())+999),('nonce','wrong'),('sub','')]:
            claims = self.claims(); claims[field] = value
            with self.subTest(field=field), self.assertRaises(AccountError):
                GoogleVerifier(CLIENT).identity_from_verified_claims(claims, 'nonce')

    def test_invalid_signature(self):
        with patch('google.oauth2.id_token.verify_oauth2_token', side_effect=ValueError('private secret')):
            with self.assertRaises(AccountError) as caught:
                GoogleVerifier(CLIENT).verify('forged', 'nonce')
        self.assertNotIn('secret', str(caught.exception))

    def test_exact_email_and_non_authoritative_domains(self):
        self.assertEqual(canonical_email(' Test.Name+alias@GMAIL.COM '), 'test.name+alias@gmail.com')
        for invalid in ['a@b@c.com', 'a b@gmail.com', '<user@gmail.com>', 'üser@gmail.com']:
            with self.subTest(email=invalid), self.assertRaises(AccountError): canonical_email(invalid)
        claims = self.claims(); claims['email'] = 'user@example.org'
        self.assertFalse(GoogleVerifier(CLIENT).identity_from_verified_claims(claims, 'nonce').authoritative_email)

class SecurityTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.path = str(Path(self.temp.name) / 'accounts.db')
        self.config = Config('https://whisper.test', CLIENT, 'owner-sub', self.path, b'k'*32)
        self.app = create_app(self.config, verifier=FakeVerifier())
        self.store = self.app.state.store
        self.owner = identity()
        self.client = TestClient(self.app, base_url=self.config.origin)

    def tearDown(self):
        self.client.close(); self.temp.cleanup()

    def headers(self):
        return {'Origin':self.config.origin,'X-CSRF-Token':self.client.cookies.get('__Host-whisper_csrf','')}

    def login(self, token='owner'):
        self.assertEqual(self.client.get('/auth/challenge').status_code, 200)
        return self.client.post('/auth/google', json={'credential':token}, headers=self.headers())

    def test_founders_seed_idempotent_and_unlimited(self):
        for person in (self.owner, identity('friend','cofounder@example.com')):
            self.store.register(person)
            grant = self.store.entitlement(person.subject)
            self.assertTrue(grant['access']); self.assertFalse(grant['payment_required'])
            self.assertIsNone(grant['expires_at']); self.assertIsNone(grant['product_usage_limit'])
        AccountStore(self.path,'owner-sub',b'k'*32)
        self.assertEqual(len(self.store.list_grants(self.owner)),2)

    def test_owner_pin_not_email_only(self):
        self.assertFalse(self.login('wrong-owner').json()['is_owner'])
        self.assertEqual(self.client.get('/api/grants').status_code,403)
        for actor in (identity('wrong'), identity(email='other@gmail.com'), identity(authoritative=False)):
            with self.assertRaises(AccountError): self.store.grant(actor,'invite@gmail.com')

    def test_admin_grant_and_revoke(self):
        self.assertTrue(self.login().json()['is_owner'])
        self.assertEqual(self.client.post('/api/grants',json={'email':'invite@gmail.com'},headers=self.headers()).status_code,200)
        self.store.register(identity('invite','invite@gmail.com'))
        self.assertTrue(self.store.entitlement('invite')['access'])
        self.assertEqual(self.client.post('/api/grants/revoke',json={'email':'invite@gmail.com'},headers=self.headers()).status_code,200)
        self.assertFalse(self.store.entitlement('invite')['access'])
        self.store.grant(self.owner,'invite@gmail.com')
        self.assertTrue(self.store.entitlement('invite')['access'])

    def test_unlisted_and_unauthenticated_cannot_admin(self):
        self.assertEqual(self.client.get('/api/grants').status_code,401)
        self.assertEqual(self.login('ordinary').json()['entitlement']['status'],'payment_required')
        self.assertEqual(self.client.get('/api/grants').status_code,403)
        self.assertEqual(self.client.post('/api/grants',json={'email':'invite@gmail.com'},headers=self.headers()).status_code,403)

    def test_pending_binding_cannot_be_stolen(self):
        self.store.grant(self.owner,'invite@gmail.com')
        self.store.register(identity('untrusted','invite@gmail.com',False))
        self.assertFalse(self.store.entitlement('untrusted')['access'])
        self.store.register(identity('invite','invite@gmail.com'))
        self.store.register(identity('reused','invite@gmail.com'))
        self.assertTrue(self.store.entitlement('invite')['access'])
        self.assertFalse(self.store.entitlement('reused')['access'])

    def test_email_change_preserves_grant(self):
        self.store.register(self.owner)
        self.store.register(identity('owner-sub','changed@gmail.com'))
        self.assertTrue(self.store.entitlement('owner-sub')['access'])
        self.assertFalse(self.store.is_owner(identity('owner-sub','changed@gmail.com')))

    def test_non_gmail_requires_verified_workspace(self):
        with self.assertRaises(AccountError): self.store.grant(self.owner,'user@example.org')
        self.store.register(identity('workspace','user@example.org'))
        self.store.grant(self.owner,'user@example.org')
        self.assertTrue(self.store.entitlement('workspace')['access'])

    def test_lifetime_protected_from_admin_and_billing(self):
        self.login()
        for email in (OWNER_EMAIL,'cofounder@example.com'):
            self.assertEqual(self.client.post('/api/grants/revoke',json={'email':email},headers=self.headers()).status_code,409)
        with self.store.connection() as db: db.execute('UPDATE accounts SET paid_until=1')
        self.assertTrue(self.store.entitlement('owner-sub')['access'])

    def test_suspension_preserves_grant(self):
        self.login()
        with self.store.connection() as db: db.execute('UPDATE accounts SET suspended=1')
        self.assertEqual(self.store.entitlement('owner-sub')['status'],'suspended')
        self.assertEqual(self.client.get('/api/me').status_code,401)
        with self.store.connection() as db: db.execute('UPDATE accounts SET suspended=0')
        self.assertTrue(self.store.entitlement('owner-sub')['access'])

    def test_csrf_origin_rejected(self):
        self.login()
        for headers in ({},{'Origin':'https://evil.test','X-CSRF-Token':self.headers()['X-CSRF-Token']},
                        {'Origin':self.config.origin,'X-CSRF-Token':'wrong'}):
            with self.subTest(headers=headers):
                self.assertEqual(self.client.post('/api/grants',json={'email':'invite@gmail.com'},headers=headers).status_code,403)

    def test_challenge_replay_and_forgery(self):
        self.assertEqual(self.login('forged').status_code,401)
        self.login()
        self.assertEqual(self.client.post('/auth/google',json={'credential':'owner'},headers=self.headers()).status_code,401)

    def test_fresh_owner_login_required(self):
        self.assertEqual(self.login('stale').status_code,401)
        self.assertEqual(self.client.get('/api/grants').status_code,401)

    def test_expiry_and_logout(self):
        self.login()
        with self.store.connection() as db: db.execute('UPDATE sessions SET expires=1')
        self.assertEqual(self.client.get('/api/me').status_code,401)
        self.login()
        self.assertEqual(self.client.post('/auth/logout',json={},headers=self.headers()).status_code,200)
        self.assertEqual(self.client.get('/api/me').status_code,401)

    def test_no_credential_echo(self):
        self.client.get('/auth/challenge')
        response=self.client.post('/auth/google',json={'credential':'private-secret','extra':'private-secret'},headers=self.headers())
        self.assertEqual(response.status_code,400); self.assertNotIn('private-secret',response.text)

    def test_cookie_and_browser_headers(self):
        response=self.login()
        self.assertTrue(any('__Host-whisper_session=' in c and 'HttpOnly' in c and 'Secure' in c and 'SameSite=strict' in c
                            for c in response.headers.get_list('set-cookie')))
        self.assertEqual(response.headers['cache-control'],'no-store')
        self.assertIn("frame-ancestors 'none'",response.headers['content-security-policy'])
        self.assertEqual(self.client.get('/admin').status_code,200)
        script=self.client.get('/admin.js').text
        self.assertNotIn('innerHTML',script); self.assertNotIn('localStorage',script)

    def test_audit_chain_tamper_and_redaction(self):
        self.store.grant(self.owner,'invite@gmail.com')
        self.assertTrue(self.store.verify_audit())
        with self.store.connection() as db:
            self.assertTrue(all('@gmail.com' not in r['payload'] for r in db.execute('SELECT payload FROM audit')))
            db.execute('UPDATE audit SET payload=? WHERE id=1',(json.dumps({'action':'tampered'}),))
        self.assertFalse(self.store.verify_audit())

    def test_unsafe_config_is_rejected(self):
        for origin, owner in [('http://whisper.test','owner-sub'),('https://whisper.test','')]:
            with self.assertRaises(ValueError):
                create_app(Config(origin,CLIENT,owner,self.path,b'k'*32),verifier=FakeVerifier())

    def test_fake_paid_flag_is_rejected(self):
        self.login()
        response=self.client.post('/api/grants',json={'email':'invite@gmail.com','paid':True},headers=self.headers())
        self.assertEqual(response.status_code,400)

    def test_session_tokens_only_stored_as_hashes(self):
        self.login()
        raw=self.client.cookies.get('__Host-whisper_session')
        with self.store.connection() as db:
            session=db.execute('SELECT * FROM sessions').fetchone()
        self.assertNotEqual(session['token_hash'],raw)
        self.assertEqual(len(session['token_hash']),64)

    def test_login_without_csrf_is_rejected(self):
        self.client.get('/auth/challenge')
        self.assertEqual(self.client.post('/auth/google',json={'credential':'owner'}).status_code,403)

    def test_body_and_host_limits(self):
        response=self.client.post('/auth/google',content='x'*20001,headers={'content-type':'application/json'})
        self.assertEqual(response.status_code,413)
        self.assertEqual(self.client.get('/admin',headers={'Host':'attacker.test'}).status_code,400)

    def test_challenge_rate_limit(self):
        for _ in range(20): self.assertEqual(self.client.get('/auth/challenge').status_code,200)
        self.assertEqual(self.client.get('/auth/challenge').status_code,429)

    def test_audit_status_is_owner_only(self):
        self.assertEqual(self.client.get('/api/audit/status').status_code,401)
        self.login()
        self.assertTrue(self.client.get('/api/audit/status').json()['chain_valid'])

    def test_sql_metacharacters_cannot_change_schema(self):
        self.store.grant(self.owner,"o'reilly@gmail.com")
        with self.store.connection() as db:
            self.assertEqual(db.execute('SELECT COUNT(*) FROM grants').fetchone()[0],3)

if __name__ == '__main__': unittest.main()
