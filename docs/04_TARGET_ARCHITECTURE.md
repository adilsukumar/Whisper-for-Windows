# Target Architecture

## Goals

- Keep the Windows experience native and responsive.
- Isolate untrusted model output from privileged execution.
- Support local, cloud, and hybrid inference without rewriting the client.
- Make coordinate transforms, protocol behavior, and safety rules independently testable.
- Package complexity behind one installer.

## Logical components

```text
User
  │ voice / text / cursor
  ▼
Windows Client
  ├─ Activation + hotkeys
  ├─ Overlay manager (one surface per monitor)
  ├─ Audio capture/playback
  ├─ Context broker
  │   ├─ Window/display metadata
  │   ├─ Windows Graphics Capture
  │   ├─ UI Automation snapshot
  │   ├─ OCR fallback
  │   └─ Sensitive-surface filter
  ├─ Session + state machine
  └─ Action executor + approval UI
        │ authenticated, versioned local protocol
        ▼
Orchestrator
  ├─ STT service
  ├─ Intent/capability router
  ├─ Context reducer
  ├─ Model adapters
  ├─ Grounding resolver
  ├─ Response/TTS stream
  ├─ Advisory risk classifier
  └─ Audit/metrics
        │
        ├─ Local models (Ollama or packaged runtimes)
        └─ Optional cloud providers
```

## Trust boundaries

1. **Windows client:** owns capture consent, the authoritative `LocalPolicyEngine`, confirmations, input injection, and local secret storage.
2. **Orchestrator:** may classify risk and propose responses/actions, but its risk labels are advisory and it cannot directly control Windows.
3. **Models:** untrusted probabilistic components. Their output must be parsed and policy-checked.
4. **Screen/UI content:** untrusted data that may contain adversarial instructions.
5. **Cloud providers:** external processors used only under the selected privacy mode.

## Client modules

### ApplicationHost

- Owns single-instance lifetime, tray icon, startup, shutdown, update coordination, and crash recovery.
- Holds long-lived resources such as mutex, cancellation root, and dependency health state.

### SessionStateMachine

Canonical states: `Idle`, `Armed`, `Listening`, `Transcribing`, `Thinking`, `Pointing`, `Speaking`, `AwaitingConfirmation`, `Acting`, `Recovering`, and `Error`.

Rules:

- Each request has a stable `session_id`, unique `request_id`, and monotonic event sequence.
- Any state can transition to `Idle` through cancellation.
- Timeouts prevent stuck states.
- Late responses for cancelled/stale requests are discarded.

### DisplayTopologyService

- Enumerates monitors, physical bounds, work areas, scale, rotation, HDR, and primary status.
- Maintains explicit transforms among physical pixels, DIPs, capture pixels, and normalized model coordinates.
- Emits topology-change events and invalidates annotations when transforms change.

### OverlayManager

- Uses one transparent top-level surface per monitor.
- Keeps the companion and annotation surfaces click-through.
- Uses a separate compact interactive window for text, settings, and confirmations.
- Clamps bubbles to safe work areas and supports reduced-motion mode.

### ContextBroker

Returns a versioned `ContextBundle`:

```json
{
  "capture_id": "uuid",
  "active_window": {"process": "...", "title": "...", "bounds": {"space": "desktop_px", "display_id": "...", "transform_version": 1}},
  "cursor": {"space": "desktop_px", "x": 0, "y": 0, "display_id": "...", "transform_version": 1},
  "displays": [],
  "captures": [],
  "uia_snapshot": {"elements": [], "focused_element_id": "..."},
  "ocr_blocks": [],
  "redactions": [],
  "timestamp": "ISO-8601"
}
```

All point/rectangle fields use the normative primitives and rounding rules in `19_DATA_AND_PROTOCOL_SCHEMAS.md`; abbreviated examples are not independent schemas.

### ActionExecutor

- Accepts only known typed actions.
- Re-resolves semantic targets immediately before execution.
- Passes every proposal through the client-side `LocalPolicyEngine`, which independently assigns risk and approval requirements.
- Captures before/after evidence and verifies postconditions.

## Orchestrator modules

### ProtocolGateway

- Validates schema and protocol version.
- Authenticates the local client.
- Supports events for partial transcript, routing decision, partial response, annotations, TTS audio, action proposal, action result, cancellation, and structured error.
- Enforces size limits and backpressure.

### CapabilityRouter

Chooses the cheapest reliable pipeline based on intent, privacy mode, available hardware/providers, context needs, latency budget, and confidence.

Example routes:

- Dictation → streaming STT → text insertion; no screenshot/model.
- General question → STT → small text model.
- “What is this?” → cursor crop + UIA neighborhood → vision model.
- “Show me Export” → UIA search → vision fallback → annotation.
- “Click Export” → grounding → policy → proposal → approval/execution.

### AdvisoryRiskClassifier

- Adds rationale and a suggested risk label to action proposals.
- Has no authority to lower the client's risk tier, bypass confirmation, expand scope, or grant a capability.
- Helps the client explain risk but is not part of the enforcement boundary.

The authoritative `LocalPolicyEngine` exists only in the Windows client. It uses deterministic rules, current user settings subject to non-overridable minimums, target context, and action history.

### ContextReducer

- Removes irrelevant UI elements and image regions.
- Preserves stable element IDs and bounds.
- Redacts sensitive content.
- Limits prompt size while retaining task-relevant state.

### GroundingResolver

- Resolves model references to UIA elements or verified image regions.
- Produces confidence and alternatives.
- Rejects out-of-range/stale coordinates.

## Protocol requirements

- JSON schema or strongly typed binary envelopes with explicit version.
- Request/response correlation IDs.
- Streaming event types rather than overloaded `done` booleans.
- Maximum audio/image/message sizes.
- Heartbeats, cancellation, reconnect, and resume behavior.
- Structured error codes: dependency unavailable, model missing, capture denied, timeout, unsupported surface, policy denied, stale target, and internal error.
- Authentication token created at installation and stored using Windows-protected storage.

## Deployment modes

### Local

All supported STT, reasoning, vision, and TTS run on device. The installer/model manager explains disk, RAM, and GPU requirements.

### Cloud

Capture/context is sent only after explicit invocation and policy filtering. Provider configuration, retention, and subprocessors must be disclosed.

### Automatic (hybrid architecture; recommended default)

Local STT, routing, UIA, redaction, and basic commands; cloud reasoning/vision when it materially improves quality; local or cloud TTS based on preference.

## Packaging direction

During prototyping, Python may remain the orchestrator. Before beta, choose between:

1. Package the Python runtime and dependencies as a signed sidecar.
2. Move orchestration to .NET and keep only model-specific native sidecars.
3. Use a Rust/native service for performance-sensitive audio/model runtime work.

Decision criteria: installer size, cold start, update complexity, security patching, diagnostics, and developer velocity. Do not rewrite solely for aesthetic reasons.
