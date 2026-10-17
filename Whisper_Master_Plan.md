# Whisper Master Plan

This is the executive plan. Detailed specifications in `docs/` are authoritative where they provide greater detail.

## Mission

Build the best Windows-native, voice-first assistant for understanding and operating desktop software. “Best” means reliable grounding, low perceived latency, excellent multi-monitor and DPI behavior, safe actions, respectful privacy, and effortless installation—not simply the largest model or fanciest animation.

## Current status

The codebase is an early prototype, not an MVP. It builds successfully and contains useful foundations, but the end-to-end voice loop is incomplete. See `docs/02_CURRENT_STATE_AND_GAPS.md` for the evidence-based inventory.

## Strategic wedge

Launch first as an interactive tutor for complex Windows software. Target workflows where visual pointing creates obvious value: creative tools, office software, engineering/CAD applications, and developer tools. Add autonomous execution only after explanation and grounding are trustworthy.

## Product pillars

1. **Windows-native grounding:** combine UI Automation, OCR, active-window metadata, cursor context, and screenshots.
2. **Realtime conversation:** streaming STT, fast routing, streaming responses, interruption, and natural TTS.
3. **Safe execution:** structured actions, previews, confirmations, audit history, pre-commit cancellation, stop controls, and recovery where possible.
4. **Private by design:** capture only on explicit activation, redact sensitive surfaces, and offer local inference.
5. **Zero setup:** one signed installer, automatic hardware detection, automatic model/provider selection, and clean updates.
6. **Delight:** polished companion behavior, useful annotations, accessibility, customization, and low distraction.

## Release sequence

| Release | Outcome | Shipping gate |
|---|---|---|
| Prototype Stabilization | Phase 0–1 developer loop; not an external release | P0/P1 roadmap gates |
| Alpha | Phase 2–3 multi-monitor tutor with benchmarked grounding | P2/P3 roadmap gates |
| Private Beta | Signed invite-only distribution with security/privacy/operations controls | P4 roadmap gate |
| Action Beta | Constrained semantic actions after independent safety review | P5 roadmap gate |
| Paid Public Beta | Commercial validation with controlled public availability | P6 roadmap gate |
| 1.0 | Supported reliable daily-use product | P7 roadmap gate |

Release names and detailed gates are defined only in `docs/14_EXECUTION_ROADMAP.md`. This summary must not introduce independent gate criteria.

## Immediate priority order

1. Complete Phase 0 (`P0-01`–`P0-11`): repository, protocol, session/state, cancellation, authentication, logging, and tests.
2. Complete Phase 1 (`P1-01`–`P1-12`): STT, routing, streaming, TTS, interruption, and recovery.
3. Pass the platform/hardware decision gate, then complete Phase 2: Per-Monitor DPI V2, topology, coordinate transforms, capture, and per-display overlays.
4. Pass the customer-discovery/app-selection gate before committing Phase 3 app-pack work.
5. Complete Phase 3: UI Automation, OCR/vision fallback, semantic grounding, benchmark, and tutor alpha.
6. Complete Phase 4: signed one-click private beta, privacy/security gate, updates, diagnostics, and onboarding.
7. Complete Phase 5: constrained action executor, non-overridable approval tiers, audit, and verification.
8. Complete Phase 6 only after retention and economics justify billing/public growth.

## Success measures

- Push-to-talk release to visible transcript: p50 under 500 ms, p95 under 1.5 s on supported hardware.
- Request to first useful response token: p50 under 2 s for text; p50 under 4 s for vision.
- Pointing/grounding: at least 90% correct target selection in every supported app/version/DPI stratum, zero wrong-monitor/out-of-bounds results, and a separately gated false-confident error rate before public beta.
- Safe action completion: at least 85% on the supported-action benchmark with zero unconfirmed destructive actions.
- Crash-free sessions: at least 99.5% in beta and 99.9% for 1.0.
- Week-four retained activated users: target at least 25% before scaling paid acquisition.

## Documentation

The documentation index, reading order, ownership expectations, and update policy are in `docs/README.md`.
