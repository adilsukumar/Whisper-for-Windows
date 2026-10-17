# Execution Roadmap

## How to use this roadmap

- Execute phases in dependency order; calendar estimates are planning ranges, not commitments.
- Each item has an observable deliverable.
- A phase is complete only when its gate passes.
- Do not start fine-tuning, a plugin marketplace, or broad autonomous agents before the private-beta gate.
- Keep a weekly changelog of completed IDs, evidence, regressions, and next risks.

## Authority and release glossary

This document is the single authoritative registry for phase/release gates. `15_DEFINITION_OF_DONE.md` supplies reusable checklists; all other documents describe desired outcomes and link here rather than adding independent blockers.

| Name | Meaning |
|---|---|
| Prototype Stabilization | Phase 0–1 developer loop; not an external release |
| Alpha | Phase 2–3 tutor experience with display correctness and benchmarked grounding |
| Private Beta | Phase 4 signed invite-only distribution with security/privacy/operations gates |
| Action Beta | Phase 5 constrained semantic actions after independent safety review |
| Paid Public Beta | Phase 6 commercial validation with controlled public availability |
| 1.0 | Phase 7 supported daily-use product |

## Phase 0 — Foundation and reproducibility

Goal: another developer or clean machine can build and diagnose the prototype.

Progress as of 2026-09-27: `P0-01`, the ignore/staging portion of `P0-02`, SDK/runtime pinning in `P0-03`, protocol portions of `P0-06`, the client state machine in `P0-07`, lifetime portions of `P0-08`, initial tests and unified checks in `P0-09`/`P0-10`, and prototype local authentication in `P0-11` are implemented. They remain subject to the full phase gate and production hardening.

### Work items

- **P0-01 Repository:** initialize Git, choose private/public visibility, create branches and review policy.
- **P0-02 Hygiene:** add `.gitignore`; remove/ignore `bin`, `obj`, `.dotnet`, Python caches, model files, installers, logs, and secrets.
- **P0-03 Dependency manifests:** pin NuGet/Python packages and record external runtime/model versions.
- **P0-04 Configuration:** add versioned typed config with development/production defaults and migrations.
- **P0-05 Logging:** structured local logs with request/session IDs and redaction.
- **P0-06 Protocol:** define versioned message envelopes, request IDs, events, structured errors, cancellation, and size limits.
- **P0-07 State machine:** replace ad-hoc UI state mutations with an explicit tested session state machine.
- **P0-08 Lifetime:** retain mutex, dispose hooks/audio/socket reliably, and make shutdown deterministic.
- **P0-09 Tests:** establish .NET and Python test projects plus CI build/syntax/unit gates.
- **P0-10 Dev scripts:** provide setup, build, test, run, and clean commands without downloading dependencies at server runtime.
- **P0-11 Local authentication:** implement per-install authenticated local IPC/loopback transport, protected secret storage, rotation/recovery, and negative tests.

### Gate P0

- Fresh documented checkout builds and tests without manual file copying.
- No secrets or large installers/model artifacts are tracked.
- Client/server negotiate protocol and handle incompatible versions clearly.
- Unauthenticated/spoofed local clients are rejected and secret lifecycle tests pass.
- Cancellation and shutdown leave no hooks/processes behind.

## Phase 1 — Complete the conversational loop

Goal: voice and text requests work end-to-end with honest realtime feedback.

### Work items

- **P1-01 Audio stream:** frame-based capture with device selection, meter, pre-roll, and errors.
- **P1-02 STT adapter:** implement and benchmark one local STT provider.
- **P1-03 STT events:** partial/final transcript protocol and UI.
- **P1-04 Stable session:** maintain one session ID and unique request IDs; add history reset.
- **P1-05 Model streaming:** stream Ollama/provider deltas and expose timing.
- **P1-06 Router v1:** avoid vision for dictation/general text and select routes deterministically.
- **P1-07 TTS fallback:** integrate a reliable built-in Windows voice.
- **P1-08 Playback:** queue/chunk speech, synchronize state, and support stop/pause.
- **P1-09 Barge-in:** new speech cancels stale model/TTS work.
- **P1-10 Recovery:** reconnect/backoff, missing-model guidance, timeouts, and retry policy.
- **P1-11 Settings shell:** audio, hotkey, inference mode, and diagnostics basics.
- **P1-12 Latency instrumentation:** record stage durations locally.

### Gate P1

- Ten consecutive voice turns complete without restarting components.
- Partial transcript and streamed response are visible.
- TTS can be interrupted instantly.
- Text-only questions do not invoke vision.
- Missing microphone/model/provider produces actionable recovery.

## Phase 2 — Correct Windows display and capture behavior

Goal: overlays and captures remain aligned across supported display configurations.

### Entry decision gate

- Select minimum Windows build/Windows 10 policy, initial architecture support, and baseline Basic/Capable hardware profiles.
- Record decisions in `17_DECISIONS_AND_OPEN_QUESTIONS.md` before fixing API/support commitments.

### Work items

- **P2-01 DPI manifest:** Per-Monitor V2 declaration and startup verification.
- **P2-02 Topology service:** monitor IDs, physical bounds, DIPs, scale, rotation, work areas, and events.
- **P2-03 Transform library:** pure typed conversions with exhaustive tests.
- **P2-04 Overlay manager:** one non-activating click-through surface per monitor.
- **P2-05 Interactive windows:** separate text/confirmation/history from overlay surfaces.
- **P2-06 WGC capture:** current window/monitor selection with metadata.
- **P2-07 Capture fallback:** controlled desktop capture for unsupported cases.
- **P2-08 Overlay exclusion:** exclude or hide overlay at capture time.
- **P2-09 Scope UX:** window/region/monitor/all-displays selection.
- **P2-10 Topology changes:** display attach/detach, DPI change, rotation, docking, and RDP.
- **P2-11 Fixture suite:** mixed-DPI/negative-coordinate screenshot and annotation tests.

### Gate P2

- Annotation alignment passes at 100–200% scale on one and mixed-DPI multi-monitor layouts.
- Text input does not block unrelated desktop clicks.
- Capture metadata identifies its exact source/transform.
- Overlay is absent from model screenshots.
- Display hot-plug invalidates/recreates surfaces without crash.

## Phase 3 — Semantic grounding and tutor MVP

Goal: point accurately and teach supported workflows.

### Entry discovery gate

- Complete at least 20 problem interviews and 10 observed workflow sessions, or document why equivalent evidence is sufficient.
- Select the first two supported applications using pain, frequency, willingness to pay, UI stability, and UIA accessibility.

### Work items

- **P3-01 UIA snapshot:** relevant elements, patterns, state, bounds, focus, hierarchy, and limits.
- **P3-02 Context reducer:** select cursor/intent-relevant UIA subtree and image crop.
- **P3-03 OCR fallback:** provider abstraction and alignment to capture coordinates.
- **P3-04 Structured grounding schema:** typed target proposals, confidence, and alternatives.
- **P3-05 Resolver:** map proposals to live elements/regions and reject stale/invalid targets.
- **P3-06 Annotation vocabulary:** pulse, box, arrow, label, spotlight, and ordered steps.
- **P3-07 Tutorial state:** next/back/repeat/why with step completion evidence.
- **P3-08 App-pack format:** supported versions, vocabulary, workflows, fixtures, prompts, and tests.
- **P3-09 First app packs:** implement two apps selected through customer research.
- **P3-10 Grounding threshold policy:** before evaluation, record the risk-derived numeric false-confident threshold, confidence calibration method, per-stratum sample minimums, and owner.
- **P3-11 Grounding benchmark:** ≥500 labeled targets across apps/themes/DPI/window sizes.
- **P3-12 Uncertainty UX:** ask or show alternatives below threshold.
- **P3-13 Privacy exclusions:** password managers, user list, and capture pause.

### Gate P3 — Alpha

- ≥90% top-1 target accuracy in every supported app/version and required DPI stratum, not merely in aggregate.
- Zero out-of-bounds or wrong-monitor annotations in test matrix.
- False-confident wrong targets stay below the defined release threshold and confidence calibration passes; ambiguous cases ask instead of guessing.
- Unsupported apps degrade to explanation without false precision.
- At least five external users complete three guided tasks each.

## Phase 4 — Productization for private beta

Goal: a nontechnical tester can install, onboard, update, and report problems.

### Work items

- **P4-01 Packaging decision:** select sidecar/runtime strategy and document rationale.
- **P4-02 Self-contained build:** no end-user SDK/Python setup.
- **P4-03 Signed installer:** per-user default, repair/uninstall, dependency/model manifest.
- **P4-04 Onboarding:** privacy, devices, hotkey, hardware benchmark, mode selection, sample task.
- **P4-05 Hardware routing:** Basic/Capable/Private profiles and automatic fallback.
- **P4-06 Tray/lifecycle:** pause, privacy mode, diagnostics, update, quit.
- **P4-07 Settings complete:** scope, exclusions, retention, voice, models, appearance, diagnostics.
- **P4-08 Update system:** signed metadata, staged channels, rollback.
- **P4-09 Crash recovery:** sidecar monitoring, safe mode, redacted report bundle.
- **P4-10 Consent metrics:** activation, latency, errors, corrections, retention without content.
- **P4-11 Legal basics:** privacy policy, terms, licenses/notices, brand clearance.
- **P4-12 Beta operations:** invitation, feedback, support, known issues, release cadence.
- **P4-13 Threat-model sign-off:** review trust boundaries, data flows, prompt injection, local transport, updates, and capture behavior.
- **P4-14 Secret lifecycle:** verify protected provider/local-auth secrets, rotation, recovery, uninstall, and log/crash redaction.
- **P4-15 Capture trust UX:** verify persistent capture/listening indicators, exclusions, per-request processing-mode disclosure, and deletion controls.
- **P4-16 Data lifecycle approval:** finalize default retention periods, storage/protection, provider retention disclosures, backup deletion timing, uninstall/account deletion, and managed-mode exceptions for every data category.

### Gate P4 — Private Beta

- Clean-machine install → first successful grounded question without developer help.
- ≥99.5% crash-free sessions in internal dogfood.
- Update and rollback pass on clean VMs.
- Privacy controls and data deletion verified.
- Authenticated local transport, protected secrets, redacted logs, and threat-model review pass.
- Installer/update artifacts are signed and verified; capture/listening indicators cannot be hidden by a model.
- Approved retention/data-lifecycle matrix and provider/backup deletion disclosures match the product and privacy policy.
- Support can diagnose common failures from redacted bundle.

## Phase 5 — Safe computer use

Goal: perform constrained tasks reliably while preserving control.

### Work items

- **P5-01 Action schema:** versioned semantic actions and pre/postconditions.
- **P5-02 Policy engine:** risk tiers, scope, confirmations, expiry, and denials.
- **P5-03 Executor:** UIA invoke/value/select/toggle/scroll and app launch.
- **P5-04 Fallback input:** guarded click/type only when semantic control is unavailable.
- **P5-05 Confirmation UI:** exact target/data/effect/reversibility preview.
- **P5-06 Verification:** observe result and prevent blind retries.
- **P5-07 Cancellation:** panic shortcut and stale-plan invalidation.
- **P5-08 Audit history:** local redacted view/export/delete.
- **P5-09 Prompt-injection suite:** screen/document attacks and policy isolation.
- **P5-10 Supported workflows:** ten bounded actions across launch applications.
- **P5-11 Action benchmark:** ambiguity, focus changes, dialogs, failures, and recovery.
- **P5-12 Security review:** independent review before enabling consequential actions.

### Gate P5 — Action Beta

- ≥85% end-to-end success on supported workflows.
- 100% of consequential actions require and record correct confirmation.
- Zero execution after cancellation or against unexpected foreground window.
- Prompt-injection suite cannot change policy or bypass confirmation.

## Phase 6 — Paid public beta

Goal: validate recurring value and sustainable economics.

### Work items

- **P6-01 Entitlements:** free/pro limits separated from safety/essential accessibility; implement and seed the two Founder Lifetime Pro grants in `22_ENTITLEMENTS_AND_FOUNDER_GRANTS.md`.
- **P6-02 Billing:** subscription, receipts, cancellation, refund handling, regional tax provider.
- **P6-03 Usage budgets:** provider cost guardrails and transparent limits.
- **P6-04 Account/privacy:** minimal account data, export/delete, device/session management.
- **P6-05 Launch site:** honest features, hardware requirements, privacy, pricing, changelog.
- **P6-06 Funnel analysis:** install, activation, first success, correction, week-one/week-four return.
- **P6-07 Support SLA:** response targets and escalation paths.
- **P6-08 Referral/education pilots:** only after retention baseline.

### Gate P6

- ≥60% activation-to-first-success.
- ≥30% week-one return among activated users.
- Week-four retention trend supports paid acquisition target.
- Positive contribution margin for paid plans after inference/payment/support costs.
- Support load remains compatible with available time.
- Both Founder Lifetime Pro grants are active, verified, billing-proof, and protected by entitlement migration tests before paid launch.

## Phase 7 — Version 1.0

Goal: dependable daily-use product with a defensible supported scope.

### Work items

- Close all P0/P1 release defects.
- Reach 99.9% crash-free sessions.
- Complete external security/privacy review.
- Publish support matrix, status/incident policy, vulnerability disclosure, and data flows.
- Refine app packs based on usage; remove low-quality unsupported claims.
- Create disaster-recovery and provider-outage fallbacks.
- Establish monthly quality and cost review.

## Parallel discovery track

Run without blocking core engineering:

- Weekly user interviews and observed tasks.
- Competitor/Windows-platform review.
- Branding/trademark/domain exploration.
- App-pack prioritization by pain, frequency, and willingness to pay.
- Pricing interviews and paid-beta experiments.

Discovery may change app focus and packaging, but cannot relax privacy/safety gates.
