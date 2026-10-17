# First 30 Days Task Board

This is the suggested first implementation sprint for a solo developer. Move unfinished work forward; do not fake completion to keep the calendar.

## Days 1–2 — Repository and baseline

- [ ] Initialize Git and make a baseline commit excluding generated/large artifacts.
- [ ] Move/delete local installers from the source tree after verifying the official download path.
- [ ] Add .NET and Python test projects.
- [ ] Record exact SDK/Python/Ollama versions.
- [ ] Create one `dev.ps1` or equivalent task runner for build/test/run.
- [ ] Capture a baseline video and stage latency log of the current prototype.

Evidence: clean status, reproducible build transcript, first CI run.

## Days 3–5 — Protocol and state

- [ ] Define shared envelope/error/event schemas from `19_DATA_AND_PROTOCOL_SCHEMAS.md`.
- [ ] Add stable application/session ID and unique request ID.
- [ ] Implement the explicit client state machine.
- [ ] Add cancellation and stale-response rejection.
- [ ] Add structured client/server logs with redaction.
- [ ] Add per-install authenticated local transport and protected secret lifecycle tests.
- [ ] Write tests for connect, request, partial events, complete, error, cancel, and reconnect.

Evidence: protocol tests and state-transition table pass.

## Days 6–9 — Speech recognition

- [ ] Benchmark at least two local STT candidates on the development machine and one modest CPU-only machine/profile.
- [ ] Record model size, cold/warm latency, word error observations, CPU/RAM/VRAM, license, and cancellation support.
- [ ] Select one prototype provider behind an interface.
- [ ] Stream audio frames and emit partial/final transcripts.
- [ ] Add microphone selection, input meter, silence/no-device states.
- [ ] Remove the audio placeholder path.

Evidence: ten varied utterances transcribe end-to-end; latency report exists.

## Days 10–12 — Streaming and speech output

- [ ] Enable streaming Ollama/model output.
- [ ] Emit response deltas separately from structured grounding/action events.
- [ ] Add built-in Windows TTS fallback.
- [ ] Implement sentence chunking, playback queue, and speaking state.
- [ ] Implement barge-in that cancels model/TTS and ignores stale output.
- [ ] Test rapid activation/cancellation loops.

Evidence: visible partial response, audible answer, instant interruption.

## Days 13–17 — Display architecture

- [ ] First pass the Phase 2 entry gate: decide minimum Windows build/Windows 10 policy, initial architectures, and baseline Basic/Capable hardware profiles.
- [ ] Add Per-Monitor DPI V2 manifest.
- [ ] Implement display topology and typed coordinate primitives.
- [ ] Write transform tests for scaling and negative coordinates.
- [ ] Build one overlay surface per monitor.
- [ ] Move text input into a separate interactive window.
- [ ] Implement WGC window/display capture plus metadata.
- [ ] Hide/exclude overlay during capture.

Evidence: screenshot/annotation fixture matrix passes on available monitor layouts; simulate others in unit tests.

## Days 18–22 — UI Automation grounding

- [ ] Before app-specific work, pass the Phase 3 discovery gate: complete required customer evidence and select the first two supported apps. If evidence is not ready, restrict this block to generic fake-app infrastructure and defer real app commitments.
- [ ] Capture focused element and relevant UIA subtree.
- [ ] Implement semantic search for named actionable controls.
- [ ] Define structured grounding response and resolver.
- [ ] Draw annotations from resolved live bounds.
- [ ] Add confidence/ambiguity behavior.
- [ ] Build a deterministic fake target app for tests.
- [ ] Label the first 100 grounding fixtures.

Evidence: “show me X” works semantically in the fake app and one real target app.

## Days 23–25 — Privacy and diagnostics

- [ ] Add visible capture/listening states and pause control.
- [ ] Add app exclusion list and at least password-manager detection.
- [ ] Add Local Only/Automatic/Cloud Allowed labels even if only Local Only works initially.
- [ ] Add diagnostics for devices, capture, orchestrator, Ollama, and models.
- [ ] Add redacted export preview.

Evidence: privacy checklist and diagnostic failure scenarios pass.

## Days 26–28 — User experience polish

- [ ] Add first-run flow for privacy, microphone, hotkey, and sample question.
- [ ] Clamp bubbles and handle long answers through a history panel.
- [ ] Add keyboard navigation, accessible names, contrast, and reduced motion.
- [ ] Add tray pause/open/quit.
- [ ] Conduct five usability sessions if testers are available; otherwise recruit them now.

Evidence: users complete first success without verbal developer instructions.

## Days 29–30 — Review and next sprint

- [ ] Run all tests, compatibility checks available, and ten golden journeys.
- [ ] Update `02_CURRENT_STATE_AND_GAPS.md` truthfully.
- [ ] Update risk register and decision log.
- [ ] Review latency, grounding accuracy, failures, and user feedback.
- [ ] Reconfirm that the selected applications still match collected evidence.
- [ ] Plan the next 30 days around P2/P3 gaps; do not start payments or broad actions yet.

## Daily routine

1. Select one roadmap ID and define today's evidence.
2. Add/adjust the failing test or fixture.
3. Implement the smallest complete vertical change.
4. Run relevant tests plus a manual cancellation/error check.
5. Record measured outcome, new risks, and documentation changes.
6. Stop adding features if a P0/P1 regression exists.

## End-of-month target

A developer-quality alpha loop: speak or type, obtain a streamed grounded response, hear TTS, interrupt safely, and point correctly on supported displays in one chosen application. This is not yet a public beta or autonomous agent.
