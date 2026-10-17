# Contributing to Whisper

## Before changing code

1. Read `docs/02_CURRENT_STATE_AND_GAPS.md` and the specialist document for the affected subsystem.
2. Link the work to a roadmap/requirement ID or document the new requirement.
3. Identify coordinate, privacy, security, accessibility, and cancellation impacts.
4. Preserve user changes and avoid committing generated artifacts, models, installers, logs, or secrets.

## Change workflow

- Keep changes small enough to review and test.
- Add/update tests and replay fixtures.
- Use typed messages/schemas across component boundaries.
- Add structured error codes and logs; do not log user content.
- Update documentation and the decision log when behavior or architecture changes.
- Include measured before/after evidence for performance/model changes.

## Pull request checklist

- [ ] Requirement/roadmap ID linked.
- [ ] Current and target behavior described.
- [ ] Tests added and passing.
- [ ] Cancellation/error/fallback paths tested.
- [ ] DPI/multi-monitor implications reviewed.
- [ ] Privacy/security and prompt-injection implications reviewed.
- [ ] Accessibility reviewed.
- [ ] Logs and telemetry contain no sensitive content.
- [ ] Documentation/changelog updated.
- [ ] No generated/large/secret files included.

## Commit guidance

Use concise imperative messages, for example:

- `fix: preserve conversation id across requests`
- `feat: add typed capture coordinate metadata`
- `test: cover mixed-DPI annotation transforms`
- `docs: record UIA grounding decision`

## Quality rule

Do not work around coordinate or timing bugs with unexplained constants. Model the relevant state explicitly and add a regression test.
