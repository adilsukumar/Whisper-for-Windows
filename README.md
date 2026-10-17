# Whisper for Windows

Whisper is a Windows-native, voice-first screen assistant that can understand what the user is looking at, point to the correct interface element, explain unfamiliar software, and—with explicit safeguards—perform actions on the user's behalf.

> Status: local developer build, not a commercial release. Native overlay, hotkeys, consented monitor capture, mixed-DPI coordinate handling, cancellation, local STT/TTS and tested lightweight vision exist. Paid-policy desktop sign-in and backend entitlement checks compile and pass fixture tests, but live Google/billing deployment, signed installer, physical multi-monitor tests, reliable action execution and accessibility/OCR-first context remain release gates. See [release status](docs/25-release-status-and-account-enforcement.md).

## Product promise

Whisper should feel like the most capable Windows expert sitting beside the user:

- Ask naturally by voice or text.
- Use the cursor, active window, UI Automation tree, OCR, and screenshot as context.
- Receive a fast spoken answer with precise on-screen guidance.
- Delegate safe tasks with previews and confirmations.
- Choose Local Only, Automatic, or Cloud Allowed processing.
- Install and work without manually configuring Python, model servers, or API keys.

## Non-goals

- Bypassing Windows security controls or UAC.
- Secretly recording the screen, microphone, or keystrokes.
- Claiming exact UI coordinates from an ungrounded vision model.
- Requiring high-end GPUs for the basic product experience.
- Shipping an unrestricted autonomous agent before safe actions are reliable.

## Quick start for developers

Prerequisites currently include .NET 8, Python, and Ollama. These are prototype requirements and must not become end-user setup requirements.

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r .\WhisperBrain\requirements.lock.txt
.\.venv\Scripts\python.exe -m pip install -r .\WhisperAccounts\requirements.lock.txt

# Optional voice transcription runtime (downloads the configured model on first use).
.\.venv\Scripts\python.exe -m pip install -r .\WhisperBrain\requirements.stt.txt

# Current prototype models; review size/license before downloading.
$env:OLLAMA_HOST='127.0.0.1:11435'
ollama pull qwen3-vl:2b-instruct-q4_K_M

# Terminal 1
.\.venv\Scripts\python.exe .\WhisperBrain\server.py

# Terminal 2
dotnet run --project .\WhisperClient\WhisperClient.csproj
```

The verified development environment uses Python 3.11.9 and .NET SDK 8.0.425. `global.json` pins the SDK and `WhisperBrain/requirements.lock.txt` pins the currently verified Python runtime dependencies.

After dependencies are installed, use the unified development entry point:

```powershell
.\dev.ps1 check
.\dev.ps1 doctor
.\dev.ps1 run-brain
.\dev.ps1 run-client
# Start the installed local developer setup with readiness/duplicate-process checks:
.\tools\Start-Whisper.ps1 -Build
# Engineering-only client package (not a full-product installer)
.\dev.ps1 package-client
```

Default prototype hotkeys:

- `Ctrl+Space`: hold to talk.
- `Ctrl+Shift+Space`: open text input.
- `Escape`: dismiss the response and annotations.

The tray menu provides Ask, Pause/Resume, Settings, and Quit. Screen sharing is off by default: text questions have a per-question checkbox; voice questions use the explicit Settings preference. Settings also control spoken responses and reduced motion, and offer a read-only local runtime/model check. Pause cancels capture and active turns; resuming does not automatically record.

The separate [accounts service](WhisperAccounts/README.md) implements Google-verified owner administration and complimentary lifetime grants. It has local automated security coverage, but is not deployed or wired into desktop commercial enforcement. Google OAuth configuration, production hosting, billing webhooks, desktop authentication, and independent security review remain launch gates.

## Documentation map

Start with [docs/README.md](docs/README.md). The most important working documents are:

- [Product vision](docs/01_PRODUCT_VISION.md)
- [Current-state audit](docs/02_CURRENT_STATE_AND_GAPS.md)
- [Product requirements](docs/03_PRODUCT_REQUIREMENTS.md)
- [Target architecture](docs/04_TARGET_ARCHITECTURE.md)
- [Execution roadmap](docs/14_EXECUTION_ROADMAP.md)
- [Definition of done](docs/15_DEFINITION_OF_DONE.md)

## Working rules

1. Never label a roadmap item complete until its acceptance criteria pass.
2. Prefer semantic Windows UI Automation actions over coordinate clicks.
3. Follow the minimum confirmation policy for consequential local and external actions.
4. Treat screen content as untrusted input and defend against prompt injection.
5. Maintain a useful cloud fallback for computers that cannot run local models well when the selected processing mode permits cloud use.
6. Measure latency, grounding accuracy, task success, crash rate, and retention.

## Repository hygiene

Vision installation versus inference verification, monitor testing, and remaining
desktop account/signing gates are documented in
[the verification guide](docs/23-vision-and-desktop-verification.md).

Git is initialized on `main`. Generated `bin/` and `obj/` files, the local `.dotnet/` installation, installers, downloaded model weights, logs, databases, and secrets are ignored. Existing local artifacts may remain on disk but must never be committed; third-party binaries still need a verified acquisition and license-record process before release.

