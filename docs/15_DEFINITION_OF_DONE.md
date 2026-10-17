# Definition of Done

Use this checklist for features, phases, and releases. “Works on my machine” is not done.

This file is a reusable checklist. The authoritative release names and gates are in `14_EXECUTION_ROADMAP.md`; if wording conflicts, the roadmap wins and this checklist must be corrected.

## Feature done

- [ ] User outcome and non-goals are documented.
- [ ] Requirement and roadmap IDs are linked.
- [ ] Architecture/trust boundary impact is reviewed.
- [ ] Error, cancellation, timeout, and recovery paths are implemented.
- [ ] Privacy and security implications are reviewed.
- [ ] Accessibility and keyboard behavior are implemented.
- [ ] Structured logs/metrics exist without user content leakage.
- [ ] Unit and integration tests pass.
- [ ] Platform/model fixtures cover relevant edge cases.
- [ ] Performance is measured against a stated budget.
- [ ] Settings/onboarding/support documentation is updated.
- [ ] Current-state audit and decision log are updated.

## Grounding feature done

- [ ] Capture/display/coordinate metadata is explicit.
- [ ] Target is resolved against current UI state.
- [ ] Bounds are validated and clamped.
- [ ] Confidence threshold and ambiguity behavior are defined.
- [ ] Mixed-DPI and secondary-monitor tests pass.
- [ ] Stale targets are rejected.
- [ ] Benchmark regression is within threshold.

## Action feature done

- [ ] Typed schema with strict validation.
- [ ] Risk tier and confirmation policy assigned.
- [ ] Target/preconditions revalidated immediately before execution.
- [ ] User can cancel before commit and stop future steps during execution; committed effects and rollback limitations are reported accurately.
- [ ] Postcondition verified.
- [ ] Retry is bounded and safe.
- [ ] Audit record is accurate and redacted.
- [ ] Prompt injection cannot authorize the action.
- [ ] Failure is never reported as success.

## Release done

- [ ] Version/changelog/release notes complete.
- [ ] CI, model benchmarks, golden journeys, and compatibility matrix pass.
- [ ] No open P0; P1 exceptions explicitly approved.
- [ ] Clean install, prior-version update, rollback, repair, and uninstall pass.
- [ ] Binaries, installer, and update metadata are signed.
- [ ] Dependency/model manifests, licenses, notices, hashes, and SBOM are current.
- [ ] Privacy policy/data-flow/support matrix match actual behavior.
- [ ] Diagnostics and support runbooks are updated.
- [ ] Staged rollout and rollback owners are identified.
- [ ] Health metrics are monitored during rollout.

## Private beta done

- [ ] A nontechnical tester can reach first success without developer intervention.
- [ ] Crash-free session target is met.
- [ ] Multi-monitor/DPI gate passes.
- [ ] Capture exclusions and deletion controls are verified.
- [ ] Authenticated local transport rejects spoofed/unauthenticated clients.
- [ ] Local/provider secrets are protected and rotation/uninstall behavior is tested.
- [ ] Logs, diagnostics, and crash paths pass redaction tests.
- [ ] Threat-model review is signed off and capture/listening indicators are verified.
- [ ] Installer and update artifacts are signed and validated.
- [ ] Feedback channel and known-issues page exist.

## Public/paid beta done

- [ ] Both owner-approved Founder Lifetime Pro grants exist in production, require no card/payment, expose all individual Pro features, and survive billing/plan migration tests.
- [ ] Action safety gate passes.
- [ ] Billing cancellation/refund and account deletion work.
- [ ] Usage limits are transparent and do not disable essential safety controls.
- [ ] Unit economics dashboard exists.
- [ ] Security vulnerability contact/process is published.

## Documentation done

- [ ] A new developer can identify current vs target behavior.
- [ ] Terms/acronyms are defined or discoverable.
- [ ] Commands are copyable and do not rely on unmentioned state.
- [ ] Claims are supported by code, test evidence, or labeled as targets.
- [ ] No contradictory architecture, privacy, or pricing statements remain.
- [ ] Links and file references are valid.
