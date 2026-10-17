"""Fail-closed paid-mode checks. No screen/audio content is sent to accounts."""
import re
from urllib.parse import urlsplit
import httpx
from protocol import ProtocolError


class EntitlementGate:
    def __init__(self, origin=None, required=False, *, transport=None):
        self.required = required
        self.origin = origin
        self.transport = transport
        if required:
            url = urlsplit(origin or '')
            if (url.scheme != 'https' or not url.hostname or url.username or url.password
                    or url.path not in ('', '/') or url.query or url.fragment
                    or url.hostname in ('localhost', '127.0.0.1', '::1')):
                raise ValueError('Paid mode requires a fixed public HTTPS accounts origin.')

    async def require(self, token):
        if not self.required:
            return
        if not isinstance(token, str) or not re.fullmatch(r'[A-Za-z0-9_-]{43}', token):
            raise ProtocolError('ACCOUNT_REQUIRED', 'Sign in to an active Whisper account.')
        try:
            async with httpx.AsyncClient(timeout=5, follow_redirects=False, transport=self.transport) as client:
                async with client.stream('GET', self.origin.rstrip('/') + '/desktop/me',
                        headers={'Authorization': 'Bearer ' + token}) as response:
                    response.raise_for_status()
                    data = bytearray()
                    async for block in response.aiter_bytes():
                        data.extend(block)
                        if len(data) > 16384:
                            raise ValueError('Invalid accounts response size')
                    import json
                    result = json.loads(data)
            entitlement = result.get('entitlement') if isinstance(result, dict) else None
            if not isinstance(entitlement, dict) or entitlement.get('access') is not True or entitlement.get('tier') != 'all_features':
                raise ValueError('Account access is inactive')
        except (httpx.HTTPError, ValueError, TypeError):
            raise ProtocolError('ACCOUNT_ACCESS_DENIED', 'Account access could not be verified. Sign in again.', True)
