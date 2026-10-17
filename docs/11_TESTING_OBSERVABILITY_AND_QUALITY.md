# Testing, Observability, and Quality

## Quality strategy

Whisper combines UI, audio, capture, models, and automation. Traditional unit tests are necessary but insufficient. Release confidence comes from deterministic component tests, replayable fixtures, hardware/display matrices, model benchmarks, and dogfooding.

## Test layers

### Unit tests

- Coordinate transforms and bounds clamping.
- Protocol serialization/version negotiation.
- State-machine transitions and cancellation.
- Highlight/action schema validation.
- Risk classification and confirmation rules.
- Conversation retention/cleanup.
- Sensitive-surface rules and log redaction.
- Routing policy with fixed inputs.

### Integration tests

- Client ↔ orchestrator handshake, auth, reconnect, and cancellation.
- Audio frames → STT events.
- Context bundle → route → model adapter → response events.
- UIA snapshot → resolver → annotation.
- Action proposal → policy → confirmation → executor → verification.
- Ollama/provider missing, slow, malformed, or unavailable.

### UI/platform tests

- Overlay placement on each monitor/DPI layout.
- Text input does not block unrelated applications.
- Keyboard-only settings and confirmation flow.
- Sleep/resume, RDP, display hot-plug, device change, and explorer restart.
- Tray/startup/update/uninstall behavior.

### Model evaluations

- Router classification benchmark.
- STT word/error rate plus domain vocabulary.
- Grounding target accuracy and false-confident rate.
- Response correctness and groundedness.
- Prompt-injection and tool-misuse suite.
- Action plan success and confirmation correctness.

### End-to-end golden journeys

1. Install → onboarding → first successful question.
2. Ask where a UI control is on a secondary mixed-DPI display.
3. Complete a supported three-step tutorial.
4. Draft text into a field, review, and send after confirmation.
5. Interrupt speech/action and immediately begin a new request.
6. Lose network/model service and recover through fallback.
7. Uninstall and verify user-data choice is respected.

## Test fixtures

- Approved screenshots with monitor/capture metadata.
- Serialized UIA trees with target labels.
- Audio clips representing languages, accents, noise, and technical terms.
- Protocol transcripts for success, failure, cancellation, and reconnect.
- Fake applications with deterministic controls and deliberate ambiguity.
- Adversarial documents/web pages containing prompt injection.

Fixtures containing user data require consent and sanitization. Prefer generated or purpose-built test content.

## CI gates

Every change:

- Formatting/static analysis.
- .NET build and unit tests.
- Python lint/type/syntax and tests.
- Protocol compatibility tests.
- Dependency/license/security checks.
- Secret scan.

Release candidate:

- Signed clean-machine install/uninstall.
- Golden journey suite.
- Display/DPI and audio-device matrix.
- Model benchmark comparison with regression thresholds.
- Update/rollback test.
- SBOM and artifact hash generation.

## Structured logging

Use event names and fields, not free-form strings. Include timestamp, app version, session/request ID, component, state, duration, error code, hardware tier, and provider/model digest. Exclude user content by default.

Suggested events:

- `app.start`, `dependency.health`, `session.start`.
- `audio.capture_started`, `stt.partial`, `stt.final`.
- `context.collected`, `route.selected`, `model.first_token`.
- `grounding.resolved`, `grounding.failed`.
- `action.proposed`, `policy.decision`, `action.completed`.
- `request.cancelled`, `request.failed`, `app.crash`.

## SLOs and dashboards

- Activation and first-success funnel.
- p50/p95 stage latency.
- Error rate by component/version/hardware tier.
- Grounding success and user correction rate.
- Action success, cancellation, and confirmation rejection.
- Crash-free sessions and update success.
- Day-1/day-7/week-4 retention for activated users.

## Bug severity

- **P0:** data loss, unconfirmed consequential action, secret exposure, security bypass, widespread startup failure.
- **P1:** frequent crash, incorrect action/pointing, capture privacy failure, unusable core loop.
- **P2:** degraded fallback, incorrect layout on a supported configuration, confusing recoverable flow.
- **P3:** cosmetic/polish issue without functional impact.

No release with open P0. Public releases require explicit sign-off for any P1.

## Manual exploratory charter

For each release, spend focused sessions trying to confuse state: rapidly press/release hotkeys, switch windows and monitors, interrupt speech, unplug audio, resize/move apps, trigger dialogs, disconnect inference, and display adversarial text. Record reproducible traces and add regression tests.
