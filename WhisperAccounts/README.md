# Whisper account service and owner console

Status: tested development implementation, **not deployed**, not connected to billing or Windows licensing. Do not market the prototype as commercially access-controlled yet.

## What is implemented

- Web owner console at `/admin`, with Google sign-in and exact-email grant/revoke controls.
- Official Google signature/audience/issuer/expiry checks, verified email, and a server-generated nonce bound to a single-use login challenge.
- Stable Google `sub` identity. Administrative access additionally requires the pinned owner subject and exact verified owner email.
- Protected founder lifetime seeds for the two approved accounts. Other invited accounts get all-feature access, no payment, no expiry, and no product usage quota. Existing grants stay bound to their original subject after email changes.
- Non-whitelisted users resolve to `payment_required` unless an authoritative, non-expired paid entitlement exists. No API lets a user set that paid state.
- Random opaque sessions stored only as hashes, secure HttpOnly cookies, strict SameSite, CSRF/origin checks, short owner sessions, security headers, body limits, and basic per-process throttling.
- Atomic grant/audit changes and HMAC-chained, pseudonymized administrative audit events.

No production switch enables fake Google login. Unit tests inject a fake verifier through the application factory; normal startup always constructs Google's verifier.

## Identity and email policy

The owner is `owner@example.com`; the second protected founder is `cofounder@example.com`. These seeds live only in the server package, not in the Windows client.

Gmail invitations can be pending until verified sign-in. For non-Gmail addresses, the account must first register with a Google-authoritative Workspace identity. A Google account using an ordinary third-party email is not sufficient evidence of current ownership of that address. Do not invent a bypass to make this easier. Add a separate verified-email or subject-based invitation flow when needed.

Addresses are trimmed/lowercased, but dots and plus aliases are **not** merged. Similar addresses do not inherit grants. A pending grant attaches once; a second Google subject with the same email cannot steal it.

Google recommends using stable `sub` and server-side verification rather than client-provided email: [Google ID token verification](https://developers.google.com/identity/gsi/web/guides/verify-google-id-token).

## Local developer setup

Run from the project root; all example package/model caches stay in the D: checkout.

```powershell
.\.venv\Scripts\python.exe -m pip install --cache-dir .\artifacts\pip-cache -r .\WhisperAccounts\requirements.lock.txt
.\dev.ps1 test-accounts
```

Configure a real Google OAuth **web** client with the exact local origin `http://127.0.0.1:8770` for this console. Add the intended test users when the Google app is in testing mode. The Windows system-browser sign-in/PKCE integration is a separate unfinished feature.

Set environment variables in your local terminal, not in source control:

```powershell
$env:WHISPER_ACCOUNTS_ORIGIN = 'http://127.0.0.1:8770'
$env:WHISPER_ACCOUNTS_DEV_HTTP = '1'
$env:WHISPER_GOOGLE_CLIENT_ID = 'YOUR_REAL_CLIENT_ID.apps.googleusercontent.com'
$env:WHISPER_OWNER_GOOGLE_SUB = 'ADMIN_DISABLED'
$env:WHISPER_ACCOUNTS_DB = '<local-project-path>
# Generate a secret without printing it to the chat or committing it.
$env:WHISPER_AUDIT_HMAC_KEY = & .\.venv\Scripts\python.exe -c "import secrets; print(secrets.token_urlsafe(48))"
.\dev.ps1 run-accounts
```

`ADMIN_DISABLED` is an operator-selected nonmatching pin: no user can administer. Sign in with the owner Google account, then inspect `/api/me` in that authenticated browser to obtain its **verified** `google_subject`. Stop the service and replace the environment value with that exact subject. Restart and sign in again. Do not derive the subject from email, accept it from a signup form, or add a public bootstrap-admin endpoint.

Persist the audit key in a secret store before treating the database as durable. Changing or losing it breaks audit verification. The `.env.example` is a template only; the application does not automatically load it.

## Owner workflow

1. Open the configured origin plus `/admin`.
2. Sign in with the pinned owner Google account. Owner sessions expire after 15 minutes; obtain a fresh token to sign in again.
3. Enter an exact target account email and grant access.
4. A new Gmail invite remains pending until that account signs in; a known verified account attaches immediately.
5. Check the table's state. Seeded founders display Lifetime protected and cannot be revoked by this UI/API.
6. Dynamic grants can be revoked with confirmation or re-enabled by granting again.
7. `/api/audit/status` is owner-only and checks the available audit chain.
8. Sign out to delete the server session and cookies.

Fresh Google token issuance is **not proof of MFA or recent password entry**. Protect the owner Google account with strong MFA/passkeys. Production administrative step-up/recovery and independent security review are still required.

## API boundary

| Endpoint | Authority | Purpose |
|---|---|---|
| `GET /auth/challenge` | Rate limited | One-time nonce and CSRF-bound challenge |
| `POST /auth/google` | Google signature + nonce + CSRF/origin | Create session |
| `GET /api/me` | Valid session | Own identity and entitlement |
| `GET /api/grants` | Fresh owner session | Private grant list |
| `POST /api/grants` | Owner + CSRF/origin | Create/re-enable complimentary grant |
| `POST /api/grants/revoke` | Owner + CSRF/origin | Revoke a non-founder grant |
| `GET /api/audit/status` | Owner session | Verify current chain |
| `POST /auth/logout` | Session + CSRF/origin | Invalidate session |

Request bodies cannot include `paid`, `admin`, arbitrary feature claims, or other extra fields. Provider error/token details are not echoed. The page uses DOM `textContent`, not email-to-HTML interpolation, and does not retain Google tokens in localStorage.

## Before hosting

Do not publicly expose the development HTTP mode. Required steps:

1. Confirm domain ownership, hosting region, operating cost budget, account-data retention, privacy notice, and backup responsibilities with the owner.
2. Register the real HTTPS origin in Google OAuth. Keep client IDs explicit; do not accept a client ID supplied by the browser.
3. Deploy behind HTTPS and a trusted proxy. Forwarded headers must be accepted only from that proxy, not arbitrary Internet clients. Verify `request.url.scheme` is HTTPS.
4. Configure public origin, actual pinned owner subject, private database path, and secret audit key via the deployment secret store. Never place the database or secret in static assets.
5. Enforce request-size, timeouts, and distributed rate limits at the edge. The included limiter is single-process and is not a distributed abuse-control system.
6. Use durable private storage, backups, tested restores, least-privilege database access, and encryption at rest. This development SQLite service is single-instance; do not assume multi-worker/multi-region correctness.
7. Anchor audit-chain checkpoints outside the database. Local HMAC verification detects altered/reordered records, but cannot prove a final suffix was not deleted without an external checkpoint.
8. Disable request-content logging, review access-log policy, and configure generic production error handling. Never log credential bodies, cookies or secrets.
9. Check Google button rendering, CSP, sign-in, session expiry, nonce/CSRF rejection, grant/revoke, founder access and logout in the real deployed browser. API tests do not prove browser/OAuth configuration.
10. Independently review owner authorization, account recovery, grant migrations, session compromise and database/secret rotation before enabling paid accounts.

## Billing and desktop gates still open

### Desktop-session server routes implemented

`POST /desktop/start` accepts a SHA-256 PKCE challenge. The app must open the
configured HTTPS accounts origin in the system browser and display the matching
eight-character code. After Google login, `POST /api/desktop/approve` requires
the browser session, CSRF token, matching code and active entitlement. The native
app polls `POST /desktop/token` with its original verifier. Approval and redemption
are serialized SQLite transactions; a request can issue only one token. Raw tokens
are never stored: the database retains hashes and one-hour expiry times.

`GET /desktop/me` checks bearer-token expiry, suspension and current entitlement;
`POST /desktop/logout` invalidates the token. Revocation changes live access rather
than trusting an old `pro=true` flag. Do not approve a code for another person's
desktop app. Never log request IDs, verifiers, tokens or approval URL queries.

These routes are tested but the WPF sign-in integration and inference-side gating
remain open. They do not yet enforce payment in the developer desktop prototype.

### Remaining launch work

- Select a billing provider with an eligible owner account; integrate checkout and **signature-verified**, idempotent webhooks before writing paid entitlement state.
- Purchase/subscription changes must never delete or override complimentary/founder grants.
- Add system-browser desktop sign-in with PKCE/state and a secure callback; no embedded password collection.
- Validate entitlement on authoritative hosted operations. Design signed, expiring offline capability claims separately; no unsigned cached `pro=true` state.
- Prototype local models/brain currently are **not** payment-gated. A user's machine cannot be treated as a trusted hosted-enforcement boundary.
- No hosted inference is configured. Complimentary grants remove payment/product quota, not provider capacity limits, anti-abuse checks, or the owner's real compute bill.
- Complete Windows 11 x64 packaging/signing, privacy, device/display tests, and product release gates. This console does not make the entire product launch-ready.
