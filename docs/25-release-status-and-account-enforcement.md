# Release status and account enforcement

## Implemented in source

- Default text/vision model is the tested Qwen3-VL 2B instruct Q4_K_M on 11435.
- One serialized model turn at a time, bounded context/output, unload after responses.
- Native cursor overlay, consented monitor capture, capture-bound annotations and cancellation.
- Local typed requests, voice recording/transcription and Windows speech synthesis.
- Google account service, stable-subject founder grants and owner-controlled complimentary grants.
- Desktop Google browser approval, PKCE, explicit matching code, one-use redemption.
- HTTPS origin embedded in paid-policy builds, redirects disabled, bounded responses.
- Desktop sessions remain in memory only. Restart requires sign-in; no passwords or
  bearer tokens are written to local settings or logs.
- Live entitlement checks before voice capture and screenshot submission. Backend paid
  mode checks again before transcription or model inference. No offline entitlement cache.
- Tray entry allows sign-in/switching accounts after expiry. Sessions last one hour.

## Developer versus paid-policy builds

The developer build is intentionally unrestricted. Do not market or distribute it
as an account-enforced paid product. Start locally with `tools\Start-Whisper.ps1 -Build`.
It refuses duplicate brain ports/client processes and never downloads models.

Paid-policy compilation:

```powershell
.\.dotnet\dotnet.exe build WhisperClient\WhisperClient.csproj `
  -p:WhisperPaidRelease=true -p:WhisperAccountsOrigin=https://YOUR-REAL-ACCOUNT-DOMAIN
```

Missing origin fails the build; malformed/non-public/non-HTTPS origins fail startup.
Configure the corresponding brain with WHISPER_REQUIRE_ACCOUNT=1 and the SAME
WHISPER_ACCOUNTS_ORIGIN. Missing/insecure paid-mode origin fails brain startup.
Do not use the developer launcher for paid distribution: it explicitly selects
developer mode. Never include sample account domains in an actual release.

## Security boundary and remaining limitations

Local model inference cannot be made tamper-proof against a user who controls the
machine and can modify executables/Python/environment. The desktop/brain checks
protect the supported product workflow, not arbitrary use of local model files.
Hosted paid services must enforce payment on the server; never trust desktop claims.
No live deployment or end-to-end Google account approval has been verified yet.
Revocations are checked at turn boundaries, not continuously during an already
running response. Failed/offline entitlement checks deny new turns.
The account tray dialog includes sign-out with server revocation. If revocation
cannot reach the server, memory-only credentials are still cleared locally and
the old server session expires within one hour.

## Required before calling the product finished

1. Provide a real HTTPS account origin, Google OAuth client ID, pinned verified owner
   subject, secure database deployment and server-owned HMAC secret.
2. Run real founder/owner/paid/unpaid sign-in tests. Owner email is
   owner@example.com; second lifetime founder is cofounder@example.com.
   Gmail text alone must never confer access without verified Google identity.
3. Integrate a billing provider and authenticated, idempotent payment webhooks;
   test purchase, cancellation, refund, expiry and replay. Billing is NOT live.
4. Bundle and license-review the Python/brain runtime. Current executable output
   is a developer client, not a complete portable product or installer.
5. Use a real code-signing certificate/provider and timestamped Windows 11 installer;
   verify signatures and install/update/uninstall on a clean VM. No signing key is supplied.
6. Test microphone permissions, device changes, cold startup, model absence and service loss.
7. Physically test mixed-DPI multi-monitor layouts, unplug/replug, rotation and capture privacy.
8. Benchmark with normal Chrome usage: CPU latency, peak RAM, pagefile pressure,
   repeated vision/text turns and actual UI localization—not just one color image.
9. Accessibility/OCR-first context and reliable approval-based computer actions are
   still future work, not delivered features. Never promise background UAC bypass.

Source compilation and fake-provider security tests are not substitutes for these gates.

## Verification record: 2026-10-05

- Brain: 55 automated tests passed.
- Account service: 36 automated tests passed.
- Desktop core: 121 assertions passed, including PKCE, origin restrictions,
  revocation and local sign-out.
- Developer and paid-policy client builds compiled without warnings/errors.
- Paid build without an account origin was rejected as intended.
- Windows cp1252 redirected-output startup crash fixed and regression-tested.
- Actual loopback request through local-auth WebSocket -> Qwen -> streamed
  completion returned READY. No screen/audio was captured in that test.
- Local developer desktop remained running/responding and connected to the brain.
- Actual image-input smoke test previously returned ready=true and the model unloaded.
- Not verified: live Google approval, live payments, signatures, installer behavior,
  physical mixed-DPI capture, voice-device UX or screen-grounding accuracy.
