import base64
import hashlib
import time
import unittest
from concurrent.futures import ThreadPoolExecutor
import test_security as fixtures
from WhisperAccounts.desktop import hashed


class DesktopTests(unittest.TestCase):
    setUp = fixtures.SecurityTests.setUp
    tearDown = fixtures.SecurityTests.tearDown
    headers = fixtures.SecurityTests.headers
    login = fixtures.SecurityTests.login

    def start(self):
        self.verifier = 'v' * 43
        challenge = base64.urlsafe_b64encode(hashlib.sha256(self.verifier.encode()).digest()).decode().rstrip('=')
        result = self.client.post('/desktop/start', json={'challenge': challenge})
        self.assertEqual(result.status_code, 200)
        return result.json()

    def approve(self, started, headers=None):
        return self.client.post('/api/desktop/approve', json={
            'request_id': started['request_id'], 'display_code': started['display_code']},
            headers=self.headers() if headers is None else headers)

    def redeem(self, started, verifier=None):
        return self.client.post('/desktop/token', json={
            'request_id': started['request_id'], 'verifier': verifier or self.verifier})

    def test_desktop_pending_and_wrong_proof(self):
        started = self.start()
        self.assertEqual(self.redeem(started).json()['status'], 'pending')
        self.assertEqual(self.redeem(started, 'x' * 43).status_code, 400)

    def test_desktop_requires_browser_session_and_csrf(self):
        started = self.start()
        self.assertEqual(self.approve(started).status_code, 401)
        self.login()
        self.assertEqual(self.approve(started, {}).status_code, 403)
        self.assertEqual(self.approve(started).status_code, 200)

    def test_desktop_unpaid_denied(self):
        started = self.start(); self.login('ordinary')
        self.assertEqual(self.approve(started).status_code, 403)

    def test_desktop_wrong_code_and_expiry(self):
        started = self.start(); self.login()
        wrong = {**started, 'display_code': '00000000' if started['display_code'] != '00000000' else 'FFFFFFFF'}
        self.assertEqual(self.approve(wrong).status_code, 400)
        with self.store.connection() as db:
            db.execute('UPDATE desktop_requests SET expires=?', (int(time.time()) - 1,))
        self.assertEqual(self.approve(started).status_code, 400)
        self.assertEqual(self.redeem(started).status_code, 400)

    def test_desktop_token_hash_replay_and_logout(self):
        started = self.start(); self.login(); self.approve(started)
        token = self.redeem(started).json()['access_token']
        headers = {'Authorization': 'Bearer ' + token}
        self.assertTrue(self.client.get('/desktop/me', headers=headers).json()['entitlement']['access'])
        with self.store.connection() as db:
            stored = db.execute('SELECT token_hash FROM desktop_tokens').fetchone()['token_hash']
        self.assertEqual(stored, hashed(token)); self.assertNotEqual(stored, token)
        self.assertEqual(self.redeem(started).status_code, 400)
        self.assertEqual(self.approve(started).status_code, 400)
        self.assertEqual(self.client.post('/desktop/logout', headers=headers).status_code, 200)
        self.assertEqual(self.client.get('/desktop/me', headers=headers).status_code, 401)

    def test_desktop_live_entitlement_revocation(self):
        self.store.grant(self.owner, 'ordinary@gmail.com')
        started = self.start(); self.login('ordinary'); self.approve(started)
        token = self.redeem(started).json()['access_token']
        headers = {'Authorization': 'Bearer ' + token}
        self.assertTrue(self.client.get('/desktop/me', headers=headers).json()['entitlement']['access'])
        self.store.revoke(self.owner, 'ordinary@gmail.com')
        self.assertFalse(self.client.get('/desktop/me', headers=headers).json()['entitlement']['access'])

    def test_desktop_expired_token_denied(self):
        started = self.start(); self.login(); self.approve(started)
        token = self.redeem(started).json()['access_token']
        with self.store.connection() as db:
            db.execute('UPDATE desktop_tokens SET expires=?', (int(time.time()) - 1,))
        self.assertEqual(self.client.get('/desktop/me', headers={'Authorization': 'Bearer ' + token}).status_code, 401)

    def test_desktop_parallel_redemption_consumes_once(self):
        started = self.start(); self.login(); self.approve(started)
        with ThreadPoolExecutor(max_workers=2) as pool:
            results = list(pool.map(lambda _: self.redeem(started).status_code, range(2)))
        self.assertEqual(sorted(results), [200, 400])
        with self.store.connection() as db:
            self.assertEqual(db.execute('SELECT COUNT(*) FROM desktop_tokens').fetchone()[0], 1)
