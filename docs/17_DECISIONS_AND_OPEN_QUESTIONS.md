# Architecture Decisions and Open Questions

## Accepted decisions

### ADR-007 — Initial release and owner grants

- **Date:** 2026-09-28.
- **Decision:** First release targets Windows 11 x64. Windows 10 and ARM64 are deferred pending explicit scope and testing.
- **Owner instruction:** `owner@example.com` manages server-side complimentary all-feature/no-product-quota grants for verified Google accounts; other accounts require paid entitlement. Preserve the two original founder accounts without payment or expiry.
- **Security:** Administration requires the owner's pinned verified Google `sub`, not email alone. No client-side admin/Pro switch. Google identity must be cryptographically verified server-side.
- **Open:** Paid provider, hosting/domain, Google OAuth setup, code signing, independent security review, and production desktop entitlement integration. None is deployed or approved by this decision.

### ADR-001 — Windows-native client

- **Decision:** Keep the primary client in C#/.NET with native Windows APIs.
- **Reason:** Best access to Windows lifecycle, UIA, display, audio, signing, and installation behavior.
- **Consequence:** Windows is the focused platform; cross-platform UI reuse is not a priority.

### ADR-002 — UIA-first grounding

- **Decision:** Prefer semantic Windows UI Automation data/actions; use vision/OCR as fallback.
- **Reason:** Semantic targets are more accurate, explainable, and verifiable than raw coordinates.
- **Consequence:** App compatibility must be measured; inaccessible canvases need specialized fallback.

### ADR-003 — Hybrid inference

- **Decision:** Support Local, Cloud, and Automatic Hybrid modes; recommend Hybrid for most users.
- **Reason:** Local-only excludes ordinary hardware; cloud-only weakens privacy and cost control.
- **Consequence:** Provider abstraction, hardware profiling, and clear data-flow UX are mandatory.

### ADR-004 — Local execution authority

- **Decision:** Models/orchestrator propose typed actions; the Windows client owns policy, approval, and execution.
- **Reason:** Prevents model/provider compromise from becoming direct desktop authority.
- **Consequence:** Protocol and executor require strict schemas and independent testing.

### ADR-005 — Do not bypass UAC

- **Decision:** UAC/secure desktop remains user-controlled.
- **Reason:** It is a core Windows security boundary.
- **Consequence:** Some elevated-app workflows are unsupported or require an explicit signed helper later.

### ADR-006 — Tutor before broad agent

- **Decision:** Launch explanation/pointing workflows before unrestricted automation.
- **Reason:** Faster trust/value validation and lower risk.
- **Consequence:** Initial marketing focuses on learning complex software.

## Proposed decisions requiring validation

### ADR-P01 — Overlay technology

Compare WPF, WinUI/Windows App SDK, DirectComposition, and a hybrid native renderer for animation/frame-time, transparency, DPI behavior, accessibility, and maintenance. Do not rewrite until benchmark evidence shows WPF cannot meet targets.

### ADR-P02 — Capture API

Use Windows Graphics Capture as primary and GDI desktop capture as fallback. Validate capture consent, overlay exclusion, protected content, window occlusion, performance, and minimum OS support.

### ADR-P03 — Orchestrator packaging

Options: bundled Python sidecar, .NET orchestration, or native/Rust sidecar. Prototype speed favors Python; beta installation/security may favor fewer runtimes. Decide by Phase 4 using measured installer size, cold start, patching, and reliability.

### ADR-P04 — Local STT/TTS runtimes

Benchmark candidate runtimes on baseline hardware before selection. Criteria: latency, accuracy/naturalness, model size, CPU/GPU load, redistribution license, cancellation, and language support.

### ADR-P05 — Installer/updater

Compare MSIX and signed bootstrapper approaches for per-user install, sidecars, models, startup registration, delta updates, rollback, SmartScreen reputation, and Store distribution.

## Open product questions

- Which two applications form the initial tutor wedge?
- What is the minimum Windows build and is Windows 10 worth supporting?
- Is ARM64 a beta requirement or post-1.0?
- What data, if any, should conversation memory retain by default?
- Should Local Only be a paid differentiator or a trust feature available broadly?
- Which cloud provider arrangement gives acceptable quality, cost, privacy, and availability?
- What name replaces or validates “Whisper” after trademark/store search?
- Which actions should remain unsupported even with confirmation?
- What weekly usage justifies subscription pricing for the target segment?

## Decision template

```markdown
### ADR-NNN — Title

- Status: Proposed / Accepted / Superseded
- Date:
- Owner:
- Context:
- Decision:
- Alternatives:
- Consequences:
- Evidence/benchmarks:
- Review date:
```
