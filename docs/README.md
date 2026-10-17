# Whisper Documentation Index

This directory is the operating system for building Whisper. Each document answers a distinct class of questions so implementation work remains traceable.

## Recommended reading order

1. [Product vision](01_PRODUCT_VISION.md) — who the product is for and why it should exist.
2. [Current state and gaps](02_CURRENT_STATE_AND_GAPS.md) — what is actually implemented today.
3. [Product requirements](03_PRODUCT_REQUIREMENTS.md) — functional and non-functional requirements.
4. [Target architecture](04_TARGET_ARCHITECTURE.md) — components, boundaries, events, and deployment modes.
5. [Windows platform engineering](05_WINDOWS_PLATFORM_ENGINEERING.md) — overlays, DPI, capture, UI Automation, input, and UAC.
6. [AI routing and grounding](06_AI_MODEL_ROUTING_AND_GROUNDING.md) — routing, context, grounding, evaluation, and fallback.
7. [Voice and realtime](07_VOICE_AND_REALTIME_PIPELINE.md) — STT, VAD, streaming, interruption, and TTS.
8. [Computer use and safety](08_COMPUTER_USE_AND_SAFETY.md) — action model, policy engine, approvals, and recovery.
9. [Privacy, security, and trust](09_PRIVACY_SECURITY_AND_TRUST.md) — threat model, data boundaries, secrets, and consent.
10. [UX, accessibility, and design](10_UX_ACCESSIBILITY_AND_DESIGN.md) — interaction states, settings, onboarding, and accessibility.
11. [Testing, observability, and quality](11_TESTING_OBSERVABILITY_AND_QUALITY.md) — test pyramid, benchmarks, telemetry, and SLOs.
12. [Packaging, release, and operations](12_PACKAGING_RELEASE_AND_OPERATIONS.md) — installer, signing, updating, support, and incidents.
13. [Business and go-to-market](13_BUSINESS_AND_GO_TO_MARKET.md) — positioning, pricing, validation, and launch.
14. [Execution roadmap](14_EXECUTION_ROADMAP.md) — ordered phases, dependencies, deliverables, and authoritative release gates.
15. [Definition of done](15_DEFINITION_OF_DONE.md) — shared completion checklist.
16. [Feature backlog](16_FEATURE_BACKLOG.md) — sequenced expansion ideas that do not belong in the MVP.
17. [Decisions and open questions](17_DECISIONS_AND_OPEN_QUESTIONS.md) — architectural decisions and unresolved choices.
18. [Developer runbook](18_DEVELOPER_RUNBOOK.md) — setup, debugging, common failures, and release commands.
19. [Data and protocol schemas](19_DATA_AND_PROTOCOL_SCHEMAS.md) — versioned messages, coordinates, context, grounding, actions, and errors.
20. [Risk register](20_RISK_REGISTER.md) — product, technical, security, legal, and business risks.
21. [First 30 days](21_FIRST_30_DAYS_TASK_BOARD.md) — day-by-day starting plan for a solo implementation sprint.
22. [Entitlements and founder grants](22_ENTITLEMENTS_AND_FOUNDER_GRANTS.md) — permanent complimentary Pro access and billing-safe implementation.

## Authority and status

| Subject | Authoritative document | Status |
|---|---|---|
| Current implementation | `02_CURRENT_STATE_AND_GAPS.md` | CURRENT; dated verification |
| Product behavior | `03_PRODUCT_REQUIREMENTS.md` | TARGET requirements |
| Trust boundaries | `04_TARGET_ARCHITECTURE.md` | TARGET architecture |
| Minimum action confirmations | `08_COMPUTER_USE_AND_SAFETY.md` | TARGET, non-overridable policy |
| Processing modes/data flow | `09_PRIVACY_SECURITY_AND_TRUST.md` | TARGET privacy contract |
| Release names/order/gates | `14_EXECUTION_ROADMAP.md` | TARGET, single gate registry |
| Completion checklists | `15_DEFINITION_OF_DONE.md` | TARGET checklist; does not redefine gates |
| Schemas/coordinates | `19_DATA_AND_PROTOCOL_SCHEMAS.md` | PROPOSED until generated types/schema land |
| Pricing and market metrics | `13_BUSINESS_AND_GO_TO_MARKET.md` | PROPOSED hypotheses |
| Complimentary lifetime accounts | `22_ENTITLEMENTS_AND_FOUNDER_GRANTS.md` | OWNER-APPROVED commitment |

## Document conventions

- **CURRENT:** verified in the repository or by a repeatable test.
- **TARGET:** intended production behavior.
- **PROPOSED:** an option requiring a recorded decision.
- **Gate:** a measurable condition that must pass before advancing.
- Requirement IDs use `FR-`, `NFR-`, `SEC-`, `UX-`, and `OPS-` prefixes.
- Roadmap items use phase and sequence identifiers such as `P2-04`.

## Updating the docs

Every meaningful pull request should update at least one of the following when applicable:

- Current-state audit when capability status changes.
- Requirements when behavior or scope changes.
- Decision log when a material tradeoff is decided.
- Runbook when setup, dependencies, or operational procedures change.
- Definition of done when a new release gate is discovered.

Do not silently turn proposals into commitments. Record the decision, rationale, alternatives, owner, and date.
