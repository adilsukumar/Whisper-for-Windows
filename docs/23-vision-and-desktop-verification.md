# Vision and desktop verification

## What “downloaded” means

Ollama's large `*-partial` file is preallocated. Its apparent file length is NOT the
number of downloaded bytes. The adjacent `*-partial-N` JSON files record retained
chunk checkpoints; the CLI can also show newer in-flight bytes. Installation is
confirmed only after the pull verifies its digest, commits the manifest, and the
model appears in `ollama list`. Do not delete the partials to fix a stalled network.

The model is pretrained by its provider; this project downloads and integrates it,
not trains or develops a replacement foundation model.

## Repeatable readiness check

Run from `<local-project-path>`:

```powershell
.\.venv\Scripts\python.exe WhisperBrain\vision_readiness.py
# After the download completes:
.\.venv\Scripts\python.exe WhisperBrain\vision_readiness.py --inference
# Explicit form (the dedicated D:-backed endpoint is now the default):
.\.venv\Scripts\python.exe WhisperBrain\vision_readiness.py --inference --ollama-url http://127.0.0.1:11435
```

The brain, desktop Settings readiness check, vision verifier, and developer doctor
now target port **11435** by default. The dedicated server must run with
`OLLAMA_MODELS=<local-project-path>`; changing a client port does not change server
storage. These changes do not restart, migrate, or modify the ordinary service
on 11434. Restart the brain/client to load the updated code after rebuilding.
The brain's explicit `OLLAMA_URL` environment override still takes precedence;
leave it unset for this configuration. Desktop Settings checks 11435.

The first command checks the exact installed model tag and advertised image-input
capability. It deliberately does not call that proof of working inference. The
second sends only an in-memory synthetic red PNG to local Ollama, requires a
completed response naming red, then asks Ollama to unload the model. It never
captures your screen, sends family photographs, deletes files, or downloads data.
Exit 0 means that smoke test passed; exit 2 means not ready/verified. Initial model
loading can take several minutes and insufficient RAM/VRAM can still cause failure.
One color test is not evidence of high-quality screen understanding or UI accuracy.

## Current desktop implementation and gates

- Cursor overlay follows the cursor's physical monitor bounds, including negative
  coordinates. Screen consent captures only the monitor selected at turn start.
- Capture IDs and coordinate-space metadata bind annotations to the original
  monitor rather than the cursor's later location. Display-topology changes cancel
  stale interactions. Mixed-DPI and unplug/replug still require physical testing.
- Browser-approved desktop-session routes use PKCE, an explicit matching display
  code, verified Google browser identity, CSRF protection, one-time request
  consumption, hashed one-hour bearer tokens, and live entitlement checks.
- Desktop sign-in and inference enforcement are now wired into paid-policy source builds.
  The developer build remains unrestricted. Live account configuration and end-to-end
  Google verification remain release gates; see `25-release-status-and-account-enforcement.md`.
- Signed installer delivery still requires a real signing certificate/service,
  timestamp/signature verification, deployment configuration, and clean Windows 11
  installation tests. No unsigned ZIP or self-signed certificate counts as complete.

## Manual verification before release

1. Complete the pull on D:, confirm `ollama list`, then run the image smoke test.
2. Test two monitors at 100% and 150/200%, with the second left/above the primary.
3. Capture on monitor A, move to B while the answer streams: annotations stay on A.
4. Unplug or rotate A mid-turn: cancel rather than draw against stale coordinates.
5. Confirm overlay/bubble/taskbar placement and screen-capture exclusion on both.
6. Wire and deploy desktop authentication; test both lifetime founders, owner
   grants/revocation, unpaid denial, suspension, expiry and offline fail-closed behavior.
7. Produce a real signed installer and verify publisher/timestamp on a fresh VM.

Automated regression tests use fake providers/local fixtures, never real Google
credentials, paid API calls or microphone/screenshot capture.
