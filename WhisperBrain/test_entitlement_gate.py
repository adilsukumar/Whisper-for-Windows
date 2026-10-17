import unittest
import httpx
from entitlement_gate import EntitlementGate
from protocol import ProtocolError, parse_client_envelope

TOKEN = 'a' * 43

class EntitlementGateTests(unittest.IsolatedAsyncioTestCase):
    async def test_live_access_then_revocation(self):
        calls = []
        def handle(request):
            calls.append(request)
            self.assertEqual(str(request.url), 'https://accounts.example.com/desktop/me')
            self.assertEqual(request.headers['Authorization'], 'Bearer ' + TOKEN)
            self.assertEqual(request.content, b'')
            return httpx.Response(200, json={'entitlement': {'access': len(calls) == 1, 'tier': 'all_features'}})
        gate = EntitlementGate('https://accounts.example.com', True, transport=httpx.MockTransport(handle))
        await gate.require(TOKEN)
        with self.assertRaises(ProtocolError): await gate.require(TOKEN)
        self.assertEqual(len(calls), 2)

    async def test_invalid_token_never_reaches_provider(self):
        def forbidden(request): raise AssertionError('must not contact provider')
        gate = EntitlementGate('https://accounts.example.com', True, transport=httpx.MockTransport(forbidden))
        for token in (None, '', 'x' * 42, 'x' * 44, '\u00e9' * 43):
            with self.subTest(token=token), self.assertRaises(ProtocolError): await gate.require(token)

    async def test_unavailable_bad_response_and_redirect_deny(self):
        for status, body in ((403, {}), (401, {}), (302, {}), (200, {}),
                             (200, {'entitlement': {'access': 1, 'tier': 'all_features'}}),
                             (200, {'entitlement': {'access': True, 'tier': 'other'}})):
            gate = EntitlementGate('https://accounts.example.com', True,
                transport=httpx.MockTransport(lambda request: httpx.Response(status, json=body)))
            with self.subTest(status=status, body=body), self.assertRaises(ProtocolError): await gate.require(TOKEN)
        def disconnected(request): raise httpx.ConnectError('secret')
        gate = EntitlementGate('https://accounts.example.com', True, transport=httpx.MockTransport(disconnected))
        with self.assertRaises(ProtocolError) as caught: await gate.require(TOKEN)
        self.assertNotIn('secret', str(caught.exception))

    async def test_response_limit(self):
        gate = EntitlementGate('https://accounts.example.com', True,
            transport=httpx.MockTransport(lambda request: httpx.Response(200, content=b'x' * 16385)))
        with self.assertRaises(ProtocolError): await gate.require(TOKEN)

    async def test_developer_mode_is_explicit(self):
        await EntitlementGate().require(None)

    def test_paid_mode_cannot_start_without_origin(self):
        for origin in (None, '', 'http://accounts.example.com', 'https://localhost',
                       'https://example.com/path', 'https://a:b@example.com', 'https://example.com?q=x'):
            with self.subTest(origin=origin), self.assertRaises(ValueError): EntitlementGate(origin, True)

    def test_wire_token_validation(self):
        envelope = {'protocol_version': '1.0', 'event_type': 'request.start', 'session_id': 's', 'request_id': 'r'}
        for token in ('', 'a' * 44, True, '\u00e9' * 43):
            with self.subTest(token=token), self.assertRaises(ProtocolError):
                parse_client_envelope({**envelope, 'account_token': token})
        self.assertEqual(parse_client_envelope({**envelope, 'account_token': TOKEN})['account_token'], TOKEN)
