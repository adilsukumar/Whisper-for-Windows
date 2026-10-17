# Changelog

All notable product changes should be recorded here. Use Keep a Changelog-style headings and semantic versioning once releases begin.

## Unreleased

### Added

- Typed request validation for field lengths, ASCII authentication tokens, valid event types, media base64 shape, and combined media budgets; malformed values receive structured errors rather than Python type failures.
- Pure model-highlight parser with bounded blocks/counts/labels, integer geometry validation, unsupported-field removal, and incomplete-fence cleanup.
- Bounded complete-message UTF-8 WebSocket assembler with split emoji/Indic text regressions. Client suite now has 99 assertions; brain suite has 36 tests.

- Read-only Settings runtime diagnostics with bounded loopback responses and timeout/cancellation handling; client harness now includes 66 assertions.
- Explicit PerMonitorV2/asInvoker application manifest and native cursor-monitor question-window placement with primary-work-area fallback.
- Self-contained Windows x64 developer publication script, fresh staging directories, per-file/ZIP checksums, and explicit non-release instructions. First publication is awaiting runtime-package downloads; no installer is claimed.
- Successfully published the explicit runtime-dependent Windows x64 developer fallback after all checks passed. This still requires the .NET 8 Desktop Runtime, local brain, and Ollama.
- Bounded vision-model pull retry script that preserves partial blobs and does not restart the service.

- Local settings for spoken responses, opt-in voice screenshots, and reduced motion; explicit per-question text screen consent.
- Separate Google-verified account service with pinned owner identity, protected founder lifetime grants, owner-managed complimentary grants, CSRF protection, expiring hashed sessions, and chained audit records. Twenty-eight local security tests; production deployment and desktop enforcement remain pending.
- Fake-microphone and highlight-clipping regression coverage: client harness now passes 55 assertions across audio, settings, privacy, geometry, state, placement, and cancellation.

- Windows tray Ask/Pause/Resume/Quit controls, with paused tooltip/menu state and icon cleanup on exit.
- Per-turn cancellation ownership and a local pause gate; client-core harness now includes 29 assertions covering pause/resume, supersession, and interruption.

- Separate keyboard-accessible question window with Enter/escape/close controls, primary-work-area clamping, and preserved pre-focus app context. The cursor overlay remains click-through during text entry.
- Dependency-free client-core regression harness with 16 placement/state assertions, integrated into `dev.ps1 check`.

- Dedicated Ollama streaming adapter and twelve async regression tests covering completion markers, malformed events, empty output, provider errors, HTTP/transport failures, cancellation, and screenshot message isolation.

- Comprehensive product, architecture, platform, AI, voice, safety, privacy, UX, quality, packaging, business, roadmap, data-schema, and risk documentation.
- Root README, contribution guide, security policy, and repository ignore rules.
- Founder Lifetime Pro entitlement requirements for the two owner-approved accounts.
- Git repository foundation on the `main` branch.
- Versioned client/orchestrator protocol with stable sessions, per-turn request IDs, streaming events, cancellation, and structured errors.
- Per-install authentication token for the loopback orchestrator.
- Content-free structured request logging and initial Python protocol/log-redaction tests.
- Pinned .NET/Python dependency manifests and unified `dev.ps1` build/test/run tasks.
- Explicit client session state machine with invalid-transition detection.
- Optional lazy-loaded faster-whisper adapter with bounded WAV validation and a typed final-transcript event.
- STT validation tests and explicit transcription lifecycle states in the Windows overlay.
- Interruptible offline Windows TTS fallback with markdown/code cleanup.
- Serialized, bounded WebSocket connection retry with backoff and per-attempt timeout.
- Developer `doctor` task for Python/STT, .NET, Ollama, and required-model readiness.

### Changed

- Fixed the actual brain startup banner's numeric-port formatting crash; added an isolated main-startup regression. CMD launchers use UTF-8 for Python output and existing D-drive runtimes/model cache.

- Failed receive loops abort only their original socket; cancelled/old connections cannot report errors against newer connections. Model output is bounded to 65,536 characters before excess chunks are forwarded.

- Screen captures check native failures, always release device contexts, dispose JPEG encoder parameters, bound allocations, and shrink capture regions to fit small displays.

- Voice capture awaits device stop, finalizes WAV headers before copying, bounds recorded input, and releases resources on cancellation or failure.
- Primary-screen cursor/highlight coordinates convert from physical pixels into window coordinates; model boxes are rejected/clipped, highlight counts/labels bounded, and annotations suppressed without screen consent. Response bubbles clamp to the overlay edges. Multi-monitor and live mixed-DPI validation remain pending.

- Pause cancels pending connection/send work, stops recording/TTS, hides the overlay and stops animation/tracking, and disables keyboard-hook callbacks. Resume does not automatically capture.
- Text-hotkey auto-repeat no longer repeatedly opens/closes input, and voice-hotkey repeats remain consumed until release.

- Cancelling a turn detaches its active request ID before awaiting the cancel send, preventing late output and old cancellation completion from clearing a newer turn.
- Dismissed voice recordings no longer submit on a later hotkey release; late response/transcript callbacks are ignored outside their expected UI states.

- Partial model output is no longer treated as complete when the stream ends early or errors. Raw provider error details are replaced with user-safe structured errors.

- Runtime doctor now compares exact installed model names and tags instead of treating table headers as missing-model evidence; six PowerShell regression cases cover this check.
- The development brain launcher defaults speech-model downloads to the repository's ignored artifacts folder, preserving an explicitly configured cache path.

- Replaced the aspirational master plan with an evidence-based executive plan.
- Reclassified the codebase as an early prototype until the documented gates pass.
- Ollama chat responses now stream through typed protocol events.
- Voice requests use local final-result transcription when the optional runtime/model is available and return structured setup/input errors otherwise.
- Narrowed the root model-cache ignore rule so the Windows `WhisperClient/Models` source directory is tracked correctly on case-insensitive filesystems.
- The application retains its single-instance mutex for the full process lifetime.
- Late responses and stale dismissal timers can no longer overwrite or cancel a newer request.
- Repeated streamed response chunks no longer trigger invalid same-state transitions.

### Known gaps

- Partial/streaming STT, TTS, semantic grounding, multi-monitor/DPI correctness, actions, installer, updater, advanced settings, production accounts/billing, and broader integration suites remain incomplete.
