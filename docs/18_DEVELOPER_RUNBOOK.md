# Developer Runbook

## Current prototype layout

- `WhisperClient/` — .NET 8 WPF client.
- `WhisperBrain/` — Python WebSocket/Ollama orchestrator.
- [`../Whisper_Master_Plan.md`](../Whisper_Master_Plan.md) — executive roadmap.
- `docs/` — specifications and operating documents.
- `.dotnet/`, `bin/`, `obj/`, installers, and model files are local artifacts and should be removed from version control after repository setup.

## Current development prerequisites

- Windows development environment.
- .NET 8 SDK (a local SDK currently exists in `.dotnet`).
- Python 3.11.9 is the currently verified interpreter; pin the supported version during Phase 0.
- Ollama for the current local model adapter.
- A microphone for voice testing.

## Install Python dependencies

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r .\WhisperBrain\requirements.lock.txt

# Required for voice transcription; text mode does not need this extra.
.\.venv\Scripts\python.exe -m pip install -r .\WhisperBrain\requirements.stt.txt
```

Production rule: the server must never install packages automatically at startup. Dependency installation belongs in setup/package build.

## Verify Ollama

The prototype defaults are `llama3.2-vision` and `llama3.1`. Confirm current model availability and licenses before standardizing them.

```powershell
ollama --version
ollama list
```

For the current prototype only, the configured models can be downloaded manually. Review disk size and exact artifact license first; these are not final product choices.

```powershell
ollama pull llama3.2-vision
ollama pull llama3.1
```

## Build

Preferred unified check:

```powershell
.\dev.ps1 check
```

Equivalent individual commands:

```powershell
dotnet restore .\WhisperClient\WhisperClient.csproj
dotnet build .\WhisperClient\WhisperClient.csproj
.\.venv\Scripts\python.exe -m py_compile .\WhisperBrain\server.py .\WhisperBrain\app_profiles.py
```

## Run prototype

```powershell
# Terminal 1
.\.venv\Scripts\python.exe .\WhisperBrain\server.py

# Terminal 2
dotnet run --project .\WhisperClient\WhisperClient.csproj
```

Current hotkeys:

- Hold `Ctrl+Space` for voice capture. The first request lazily loads/downloads the configured faster-whisper model and can therefore take substantially longer.
- Press `Ctrl+Shift+Space` for text input.
- Press `Escape` to dismiss.

Completed model responses are spoken with the installed default Windows voice. Starting a new request or pressing `Escape` cancels playback. Voice/rate/device settings and neural voices remain roadmap work.

## Known failure modes

### Cannot connect to Ollama

- Verify Ollama is running and listening on the configured local endpoint.
- Run `ollama list` and verify the configured model exists.
- Check `OLLAMA_URL`, `WHISPER_VISION_MODEL`, and `WHISPER_TEXT_MODEL`.
- Do not describe backend connection errors as assistant responses in production.

### Model output stops midway

- `MODEL_STREAM_INCOMPLETE`: the connection ended without Ollama's terminal `done=true` event. Partial text is not considered a completed answer.
- `MODEL_CONNECTION_LOST`: the local model HTTP stream failed; retry after checking the Ollama service.
- `MODEL_INFERENCE_FAILED`: Ollama sent a streamed error. Raw provider details are deliberately not forwarded because they may contain local paths or user content.
- `MODEL_INVALID_RESPONSE` or `MODEL_EMPTY_RESPONSE`: the provider returned malformed events or no usable answer.
- A retry is user initiated; inference is not automatically replayed after partial output. Completed conversation history is updated only after the adapter succeeds.

### Client cannot connect to brain

- Verify port `8765` is free and the Python server started.
- Confirm `WHISPER_PORT` and the client URL match.
- Local protocol events require a per-install token. Production ACL/protected-storage hardening and reconnect/backoff remain open.

### Overlay/highlight is misplaced

This is a known architectural gap on scaled or multi-monitor systems. Capture the monitor layout and scale settings for a future fixture; do not patch with arbitrary offsets. Implement the transform architecture in Phase 2.

### Text box blocks desktop interaction

Text entry now lives in a separate small window; the overlay remains click-through. Automated placement/state assertions pass, but complete this desktop acceptance check before claiming the UX fixed:

1. Run the brain and client, then use `Ctrl+Shift+Space` from another app. Only the small question window should receive input.
2. Click another app outside that window; the underlying app should receive the click without closing Whisper.
3. Reopen the question window and move the cursor; the typing window must stay stationary.
4. Type a question and press Enter once; the input closes, one request is sent, and the prior foreground-app context is retained.
5. Press Escape, use the close button, and toggle the hotkey again; each should close without submitting.
6. Open at every primary-screen corner and with the taskbar on different edges. The window should remain in the primary work area.
7. Open text input during a slow response, then start voice or dismiss. Cancelled output must not replace the newer mode; dismissing voice before key release must not submit audio.

Current scope: primary-monitor placement only. This is not evidence of correct mixed-DPI/multi-monitor capture or overlay exclusion from screenshots.

### Voice transcription is unavailable

- Install `WhisperBrain/requirements.stt.txt` in the active environment.
- Check `WHISPER_STT_MODEL` (default `base`), `WHISPER_STT_DEVICE` (default `cpu`), and `WHISPER_STT_COMPUTE_TYPE` (default `int8`).
- Set `WHISPER_STT_MODELS_DIR` to control the model cache location.
- `dev.ps1 run-brain` defaults this cache to `artifacts\models\stt` inside the repository (on D: in this development checkout), unless you explicitly override it. Running `server.py` directly without the variable uses the transcription library's default cache instead.
- The model is acquired lazily on first use; network failure or an incompatible device/compute type returns `STT_MODEL_UNAVAILABLE`.
- `INVALID_AUDIO`, `AUDIO_TOO_LARGE`, and `NO_SPEECH_DETECTED` are actionable recording/input failures.

## Tray and pause acceptance checks

The tray now offers Ask Whisper, Pause/Resume, and Quit; double-click opens text input when active. It uses the built-in Windows notification-icon component; a branded icon is still pending.

1. Pause during recording: the input device should stop, speech should stop, input/highlights should close, and the overlay should disappear. Confirm the tray tooltip says Paused.
2. While paused, both hotkeys should pass through without opening input or capturing. Ask is disabled. No new request should be sent.
3. Pause while connecting or awaiting inference. Confirm cancellation does not later send the pending question or display a stale result. Already transmitted data cannot be recalled; the server receives cancellation when connected.
4. Resume: overlay/hotkeys return, but microphone/screen capture must not start until explicit input.
5. Hold the text hotkey: one press should open/close once, not oscillate with keyboard auto-repeat.
6. Quit while active and while paused. Confirm process, audio, hooks, input window, and tray icon are gone; relaunch should acquire the single-instance mutex.
7. Repeat pause/resume 20 times; check for duplicate hooks/icons, retained devices, or increasing memory.

Automated tests cover the pause/turn ownership gate, not actual device/desktop behavior. Keep this manual verification pending until performed. Pause is local to this process; it does not unload Ollama or stop another app's downloads, and it is not yet persisted across launches.

## Debug information to collect

- App/orchestrator/model versions.
- Windows build and architecture.
- Monitor bounds, scale factors, orientation, and primary display.
- Audio device name and failure code.
- Request/session ID and stage durations.
- Structured error code and stack trace.
- Reproduction steps.

Do not collect screenshots, transcripts, window titles, paths, tokens, or audio without explicit need and consent.

## Development discipline

- Add a failing test/fixture for every reproducible bug before or with the fix.
- Never mix physical pixels and WPF DIPs without an explicit typed transform.
- Never execute raw model-generated shell/input commands.
- Never log provider keys or user content.
- Never mark a roadmap item complete without the documented gate evidence.
- Update current-state and decision docs with material changes.

## Release preparation

### Transport/schema hardening (2026-09-29)

The full check now includes 99 client assertions, 36 brain tests, 28 account security tests, and six readiness cases. New tests cover fragmented emoji/Indic UTF-8, message overflow/reset, typed request fields, Unicode/oversized auth failures, combined media bounds, defensive highlights, and excessive model output.

Current limits: text 32,768 characters; window context 512; encoded audio 14 MiB; encoded screenshot 8 MiB; combined encoded media 16 MiB. The WebSocket request budget adds 512 KiB for escaped text and metadata. Requests beyond the WebSocket frame budget can still be rejected at the transport layer before a structured error can be returned. Base64 shape checks are not full image validation; WAV validation remains in STT. Client server-event assembly is capped at 512 KiB and model answer text at 65,536 characters. Model highlights allow only box/pulse, up to 32 items, integer coordinates within 32,768 and labels up to 200 characters. These are technical safeguards, not paid usage quotas or grounding-confidence checks.

The previous 2026-09-28 developer package predates the new transport fixes. Rebuild with the explicit runtime-dependent publication command before using it to verify these changes. Real Unicode dictation, live WebSocket disconnect/reconnect, DPI, and microphone integration checks remain pending.

An updated runtime-dependent client was published after the full check at `artifacts/developer-client/20260929-104314-47a05891.zip`. It contains the transport fixes; use the current source checkout for the matching brain changes. No installer or production billing is included.

### Latest verification and manual checks (2026-09-28)

Additional runtime/capture checks bring the client suite to 66 assertions. Use Settings → Check local runtime to query only the loopback model list and current brain connection; it never starts a model pull or captures user content. A model appearing in the list proves installation, not usable performance.

Open text input while the cursor is on each monitor, including monitors to the left/above primary. Verify work-area clamping and monitor-scale changes. The input window now uses physical native bounds; overlay/capture still use the primary display. The explicit PerMonitorV2 manifest requires real mixed-DPI regression testing before release.

`dev.ps1 package-client` creates fresh unsigned developer client artifacts when the .NET runtime packages can be restored. See the packaging document for limitations. The initial attempt timed out downloading official runtime packages; the normal project build remains verified.

For an explicit model download, follow `tools/README.md` using the official Ollama CLI and your own service-side cache configuration. Machine-specific retry/download scripts from the private development installation are not included in this public edition. Download completion alone does not establish inference readiness.

During the latest download session Wi-Fi was up and DNS recovered, but earlier NuGet requests failed name resolution and stalled. This is evidence of intermittent connectivity, not proof of a defective Wi-Fi chip. No adapter, DNS, Google Drive, or firewall settings were changed.

`dev.ps1 check` now runs 55 client assertions, 22 brain tests, 28 account security tests, six readiness cases, compilation, and whitespace checks. Install both pinned brain/account dependency manifests before running it. The account TestClient emits a dependency deprecation warning; the Windows build itself has zero warnings/errors.

1. Open Settings from the tray. Save each option, restart the client, and verify persistence. Keep voice screen sharing off initially.
2. Submit text with screen sharing unchecked: no screenshot or active-window context should be sent. Check it for one question, then reopen input and confirm it defaults unchecked again.
3. Hold/release voice input; verify the final utterance transcribes. Dismiss or pause during stop/transcription, then immediately ask another question; no old result may replace the new turn.
4. At primary-monitor scales 100%, 125%, 150%, and 200%, verify companion placement and boxes against known UI elements. Compare physical screenshot positions to window positions. These are pending manual checks, not passed automated DPI tests.
5. Ask near all display edges; verify ordinary response bubbles stay visible. Long responses, secondary monitors, mixed scaling, monitor disconnect, and resolution changes remain release blockers until tested/fixed.
6. Follow `WhisperAccounts/README.md` for actual OAuth configuration, pinned owner identity and whitelist checks. Mock-verifier tests are not evidence that deployed Google OAuth works. Do not deploy publicly before its listed production gates pass.

`dev.ps1 doctor` currently reports `llama3.2-vision` missing while `llama3.1:latest` is installed. A successful code check does not imply model readiness. Do not delete partial model files or restart the pull as a troubleshooting shortcut.

Follow `12_PACKAGING_RELEASE_AND_OPERATIONS.md` and `15_DEFINITION_OF_DONE.md`. The current debug executable is not a distributable product.
