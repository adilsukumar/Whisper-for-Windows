# Product Requirements

## Requirement policy

Every requirement must map to at least one test or metric before it can be marked complete. “Supported” means the behavior works on the documented Windows/hardware matrix and fails gracefully elsewhere.

## Core functional requirements

### Activation and input

- **FR-001:** The user can invoke push-to-talk globally with a configurable hotkey.
- **FR-002:** The user can invoke a compact text input without blocking unrelated desktop interaction.
- **FR-003:** The user can cancel capture/inference/speech immediately and stop future action steps immediately; effects already past an irreversible commit point may remain and must be reported.
- **FR-004:** The product detects hotkey conflicts and offers alternatives.
- **FR-005:** The user can choose an input device and see an input-level test.

### Context collection

- **FR-010:** Capture occurs only after an explicit user invocation unless the user enables a clearly disclosed workflow mode.
- **FR-011:** The request context can include active process, window title, cursor position, monitor identity, capture dimensions, DPI scale, and UIA elements.
- **FR-012:** The user can select current window, current monitor, region, or all monitors as capture scope.
- **FR-013:** Sensitive applications and user-defined exclusions prevent capture.
- **FR-014:** The overlay is excluded or hidden from captured content.
- **FR-015:** The product handles unsupported/protected content without pretending it saw the screen.

### Conversation

- **FR-020:** Speech is transcribed with partial and final results.
- **FR-021:** Assistant responses stream to the interface and TTS pipeline.
- **FR-022:** The user can interrupt speech and immediately issue a new request.
- **FR-023:** Conversation identity persists across requests and can be cleared.
- **FR-024:** History retention is configurable and defaults to local minimal retention.
- **FR-025:** Responses disclose uncertainty when context is insufficient.

### Grounding and guidance

- **FR-030:** The assistant can identify actionable UI elements through UIA where available.
- **FR-031:** Vision/OCR fallback can ground non-accessible interfaces.
- **FR-032:** Every annotation references a capture, monitor, coordinate space, and confidence.
- **FR-033:** Drawing supports point, box, arrow, path, label, and ordered step markers.
- **FR-034:** Annotations remain correctly aligned during DPI/display changes or are safely invalidated.
- **FR-035:** The user can ask “why,” “next,” “repeat,” and “show me” without restating context.

### Actions

- **FR-040:** Actions use a versioned typed schema rather than free-form commands.
- **FR-041:** Semantic UIA operations are preferred over input injection.
- **FR-042:** Actions follow the non-overridable minimum confirmation table in `08_COMPUTER_USE_AND_SAFETY.md`; users may make policy stricter but not weaker.
- **FR-043:** Actions have timeouts, pre-commit cancellation, immediate stop of future steps, bounded safe retries, and postcondition checks; rollback is offered only when a verified inverse exists.
- **FR-044:** The user can inspect a local audit history of requested and executed actions.
- **FR-045:** Whisper never bypasses the UAC secure desktop or user consent.
- **FR-046:** When an action target is ambiguous, Whisper asks or points instead of guessing.

### Settings and onboarding

- **FR-050:** First run tests microphone, speaker, display capture, model/provider, and hotkey.
- **FR-051:** Hardware detection recommends Local Only, Automatic, or Cloud Allowed processing using the normative data-flow contract.
- **FR-052:** Settings include privacy, exclusions, retention, devices, hotkeys, appearance, voice, and diagnostics.
- **FR-053:** The user can export/delete local history and diagnostics.
- **FR-054:** The application updates safely with release notes and rollback on failed update.
- **FR-055:** Owner-approved Founder Lifetime Pro accounts receive all current/future individual Pro features without subscription charge, expiry, renewal, or card requirement according to `22_ENTITLEMENTS_AND_FOUNDER_GRANTS.md`.

## Non-functional requirements

### Performance

- **NFR-001:** Overlay animation maintains 60 FPS at p95 on supported hardware when idle.
- **NFR-002:** Idle CPU averages below 1% and memory remains below an agreed budget measured on the baseline device.
- **NFR-003:** Push-to-talk release to visible transcript is p50 <500 ms and p95 <1.5 s.
- **NFR-004:** Text request to first token is p50 <2 s; vision request p50 <4 s in automatic mode.
- **NFR-005:** Capture and context collection do not freeze the UI thread.

### Reliability

- **NFR-010:** Beta achieves at least 99.5% crash-free sessions; 1.0 reaches 99.9%.
- **NFR-011:** Backend/model restarts recover automatically without restarting the UI.
- **NFR-012:** No request can remain indefinitely in Listening, Thinking, Speaking, or Acting state.
- **NFR-013:** Network or model failures produce actionable diagnostics and safe fallback.

### Compatibility

- **NFR-020:** Define and test minimum supported Windows 10/11 builds and x64/ARM64 policy.
- **NFR-021:** Test 100%, 125%, 150%, 175%, 200%, and mixed-DPI displays.
- **NFR-022:** Test primary display changes, negative monitor coordinates, RDP, docking, sleep/resume, and display hot-plug.
- **NFR-023:** Support keyboard-only and screen-reader navigation for settings and confirmations.

### Privacy and security

- **SEC-001:** Screen content is treated as untrusted data, never as authority to change policy.
- **SEC-002:** Provider secrets are stored with Windows-protected storage and never logged.
- **SEC-003:** Local control endpoints authenticate clients and bind only to intended interfaces.
- **SEC-004:** Logs redact screen text, audio, tokens, secrets, and user content by default.
- **SEC-005:** Installers, updates, and production binaries are signed and verified.
- **SEC-006:** Third-party dependencies and model artifacts have pinned versions, hashes, licenses, and provenance.
- **SEC-007:** Sensitive external side effects require explicit, contextual confirmation.

## Launch acceptance scenarios

1. On a mixed-DPI two-monitor setup, ask “where is Export?” and receive a correctly aligned highlight on the non-primary display.
2. In a supported app, ask for a three-step workflow and advance step-by-step without recapturing irrelevant displays.
3. Disconnect Ollama or the network during a request and recover without a frozen overlay.
4. Display malicious text instructing the assistant to ignore safety and verify that policy remains unchanged.
5. Ask Whisper to send or delete something and verify a precise preview appears before execution.
6. Switch microphone, sleep/resume, dock/undock, and repeat a request successfully.
7. Remove all models/providers and verify onboarding explains how to restore a working mode.
