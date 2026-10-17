"""Only verified Google claims may cross this module's identity boundary."""
from dataclasses import dataclass
import hmac
import time


class AccountError(Exception):
    def __init__(self, code: str, message: str, status: int = 400):
        super().__init__(message)
        self.code, self.message, self.status = code, message, status


def canonical_email(email: str) -> str:
    if not isinstance(email, str):
        raise AccountError('INVALID_EMAIL', 'Enter a valid email address.')
    email = email.strip().lower()
    if len(email) > 254 or not email.isascii() or email.count('@') != 1 or any(c.isspace() for c in email):
        raise AccountError('INVALID_EMAIL', 'Enter a valid email address.')
    local, domain = email.split('@')
    if not local or '.' not in domain or not domain or any(c in email for c in '<>\\/"'):
        raise AccountError('INVALID_EMAIL', 'Enter a valid email address.')
    # Never normalize Gmail dots/plus aliases: grants are exact approved addresses.
    return email


@dataclass(frozen=True)
class VerifiedIdentity:
    subject: str
    email: str
    authoritative_email: bool
    issued_at: int


class GoogleVerifier:
    def __init__(self, client_id: str):
        if not client_id or not client_id.endswith('.apps.googleusercontent.com'):
            raise ValueError('Configure the real Google OAuth client ID.')
        self.client_id = client_id

    def verify(self, credential: str, nonce: str) -> VerifiedIdentity:
        if not isinstance(credential, str) or not 1 <= len(credential) <= 16384:
            raise AccountError('INVALID_LOGIN', 'Google login could not be verified.', 401)
        from google.oauth2 import id_token
        from google.auth.transport.requests import Request
        try:
            # The official verifier validates Google's signature, audience and expiry.
            claims = id_token.verify_oauth2_token(credential, Request(), self.client_id)
        except Exception as error:
            raise AccountError('INVALID_LOGIN', 'Google login could not be verified.', 401) from error
        return self.identity_from_verified_claims(claims, nonce)

    def identity_from_verified_claims(self, claims: dict, nonce: str) -> VerifiedIdentity:
        """Internal post-signature validation; never an API for client-supplied claims."""
        now = int(time.time())
        if (claims.get('iss') not in {'accounts.google.com', 'https://accounts.google.com'} or
            claims.get('aud') != self.client_id or claims.get('email_verified') is not True or
            not isinstance(claims.get('exp'), int) or claims['exp'] <= now or
            not isinstance(claims.get('iat'), int) or claims['iat'] > now + 30 or
            not isinstance(claims.get('nonce'), str) or not hmac.compare_digest(claims['nonce'], nonce)):
            raise AccountError('INVALID_LOGIN', 'Google login could not be verified.', 401)
        subject = claims.get('sub')
        if not isinstance(subject, str) or not subject or len(subject) > 128:
            raise AccountError('INVALID_LOGIN', 'Google login could not be verified.', 401)
        email = canonical_email(claims.get('email'))
        authoritative = email.endswith('@gmail.com') or bool(claims.get('hd'))
        return VerifiedIdentity(subject, email, authoritative, claims['iat'])
