# Risk Register

Review monthly and before every release gate. Probability/impact use Low, Medium, High, Critical.

| ID | Risk | Probability | Impact | Early signal | Mitigation | Trigger/owner |
|---|---|---:|---:|---|---|---|
| R-01 | Official competitor launches Windows first | High | High | Windows beta/launch announcement | Differentiate on native grounding, safety, app packs; ship narrow beta quickly | Revisit positioning immediately |
| R-02 | Local models too slow on typical hardware | High | High | Vision p50 above target; setup churn | Automatic default, hardware benchmark, smaller routes, permitted cloud fallback | Model/platform owner |
| R-03 | Pointing is frequently wrong | High | Critical | Correction rate and benchmark failure | UIA-first resolver, confidence/alternatives, no false precision | Block Alpha gate |
| R-04 | Unconfirmed consequential action | Medium | Critical | Policy/confirmation test failure | Local policy engine, typed tools, security review, staged rollout | Disable actions immediately |
| R-05 | Prompt injection controls action | High | Critical | Adversarial test succeeds | Structured trust boundaries, intent binding, confirmation | Security owner |
| R-06 | Mixed-DPI/multi-monitor bugs persist | High | High | Wrong-monitor fixtures fail | Explicit transforms, per-monitor overlays, matrix tests | Block private beta |
| R-07 | Installer/SmartScreen harms activation | Medium | High | Install abandonment/support tickets | Signing, reputation, minimal bootstrapper, Store evaluation | Release owner |
| R-08 | Python/Ollama packaging becomes fragile | Medium | High | cold start/update/AV failures | Package/runtime ADR, self-contained sidecar or migration | Decide by Phase 4 |
| R-09 | Model/dependency license blocks redistribution | Medium | High | missing/changed license terms | Per-artifact review, manifests/notices, downloadable optional models | Legal/release owner |
| R-10 | Cloud inference costs exceed price | Medium | High | negative contribution margin | routing budgets, caching where safe, limits, pricing tests | Weekly cost review |
| R-11 | Privacy incident from screen/log capture | Medium | Critical | sensitive content in diagnostics | exclusions, redaction, content-free telemetry, review | Incident response |
| R-12 | Brand “Whisper” is unavailable/confusing | High | Medium | trademark/store/domain conflicts | Early brand clearance and rename before launch | Product owner |
| R-13 | Feature breadth delays useful launch | High | High | roadmap items started across many phases | phase gates, narrow apps, defer backlog | Weekly scope review |
| R-14 | Side-hustle support burden is unsustainable | Medium | High | high tickets/user; hardware-specific failures | support matrix, diagnostics, staged invites, pricing | Pause growth |
| R-15 | Low retention despite impressive demos | Medium | Critical | weak week-one/week-four return | observed workflows, tutor wedge, cut unused features | Revisit ICP/problem |
| R-16 | Platform/API change breaks capture/UIA | Low | High | Windows preview regressions | compatibility CI, adapters, fallback, staged updates | Platform owner |
| R-17 | Security software flags hooks/injection | Medium | High | AV detections and user distrust | minimize hooks, sign, semantic UIA, vendor submission | Release/security owner |
| R-18 | Dataset/fine-tuning violates consent/IP | Medium | Critical | unclear provenance | do not train by default; consented licensed dataset and records | Block training |

## Risk review questions

- Did probability or impact change based on evidence?
- Did a mitigation become a roadmap commitment?
- Is a gate missing a risk-specific test?
- Has a new external dependency, data flow, or privilege been introduced?
- Does a risk now exceed the acceptable scope of a side business?
