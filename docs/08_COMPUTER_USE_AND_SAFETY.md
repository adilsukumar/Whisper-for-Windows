# Computer Use and Safety

## Core rule

Models propose; deterministic policy decides; the Windows client executes. No model/provider may directly control mouse, keyboard, files, processes, accounts, or external services.

## Action schema

Initial action families:

- `uia.invoke(element_ref)`
- `uia.set_value(element_ref, value)`
- `uia.select(element_ref, option)`
- `uia.toggle(element_ref, desired_state)`
- `uia.scroll(container_ref, direction, amount)`
- `window.activate(window_ref)`
- `app.launch(app_id)`
- `input.click(point_ref)` as fallback only
- `input.type(target_ref, text)` as fallback only
- `file.open(path_ref)` under allowed scope

Each action includes request ID, rationale, target identity, expected preconditions/postconditions, risk class, expiry, and whether confirmation is required.

## Risk tiers

| Tier | Examples | Default policy |
|---|---|---|
| 0 Observe | Inspect UI, screenshot, search visible controls | Execute after invocation |
| 1 Reversible local | Open menu, select tab, scroll, type into unsent field | Execute with visible indicator; allow undo |
| 2 Consequential | Send message, submit form, overwrite file, change settings | Show exact preview and require confirmation |
| 3 Sensitive/high impact | Purchase, delete data, publish, security/account change | Strong confirmation; re-authentication where available; may be unsupported |
| 4 Prohibited | Bypass UAC/security, steal secrets, hide activity | Refuse |

## Normative confirmation decision table

The client-side `LocalPolicyEngine` is authoritative. User settings may require more confirmation but cannot weaken these minimums.

| Effect/context | Minimum behavior |
|---|---|
| Read/inspect visible state within invoked scope | No additional confirmation |
| Navigate/scroll/open a reversible view in the expected app | Visible execution indicator; immediate cancel |
| Enter text into an unsent field | Preview when text is sensitive/long or target is ambiguous; never auto-submit |
| Send/publish/submit/message/form | Exact content, destination/account, and `Send/Submit` confirmation immediately before commit |
| Create/overwrite/move/delete a file | Exact path/effect/reversibility confirmation; deletion prefers recycle/recoverable operation |
| Purchase/payment/subscription | Item, merchant, amount, account, recurrence, and explicit purchase confirmation; re-authentication where supported |
| Account/security/permission change | Strong confirmation and re-authentication; some operations remain unsupported |
| Elevated/UAC operation | Stop and wait for the user on the secure desktop; never automate consent |
| Target/app/account changed after approval | Approval invalid; show a new preview |

Approvals are bound to exact action IDs, target identity, arguments/content hash, account/app, and a short expiry. They do not authorize unspecified follow-up effects. Grouped approval is allowed only for a fully enumerated homogeneous set with one combined preview; new or changed actions require new approval.

Cancellation before a commit point prevents execution. After an irreversible commit point, “Stop” prevents future actions and Whisper reports what already occurred; it must not promise rollback. Where a verified inverse exists, offer it as a separate user-approved action.

## Plan-execute-verify loop

1. Interpret the user's request.
2. Generate a short typed plan.
3. Resolve current targets.
4. Evaluate each action through policy.
5. Present grouped preview for actions requiring approval.
6. Revalidate foreground window and target.
7. Execute one bounded action.
8. Observe postcondition using UIA/capture.
9. Continue, recover, ask, or stop.
10. Store a redacted local audit entry.

## Confirmation UX

A confirmation must state:

- What will happen.
- Which app/account/document is affected.
- The exact text/data to be sent or changed.
- Whether the operation is reversible.
- Why Whisper believes it matches the request.

Buttons must be explicit: `Send`, `Delete file`, `Apply changes`, or `Cancel`; avoid vague `OK` for consequential actions.

## Ambiguity handling

- Multiple matching elements: highlight candidates and ask.
- Stale/moved element: reacquire once, then ask.
- Unexpected dialog: pause and explain.
- Foreground app changed: stop before input injection.
- Postcondition failed: bounded recovery, never blindly repeat a consequential action.
- User interrupts: cancel pending and future actions immediately.

## UAC and elevated applications

Whisper does not bypass UAC or interact with the secure desktop. It may explain the prompt and wait for the user. A future elevated helper must be signed, narrowly scoped, explicitly launched, and independently threat-modeled.

## Audit log

Store locally by default:

- Timestamp, request ID, app identity, action type, target summary, policy decision, confirmation result, execution result, and postcondition status.
- Redact typed secrets and user content where possible.
- Provide clear/delete/export controls.
- Never describe a failed action as successful.

## Safety test suites

- Prompt injection rendered inside documents/websites.
- Lookalike and overlapping controls.
- Window/focus switch between planning and execution.
- Disabled/hidden/stale controls.
- Destructive operations without confirmation.
- Repeated confirmation fatigue and grouped approvals.
- Cancellation during execution.
- Network/model failure after partial plan.
- UAC secure desktop and elevated target processes.

## Rollout

1. Read-only pointing.
2. Reversible semantic navigation.
3. Drafting into unsent fields.
4. Confirmed sends/submissions.
5. File/system changes with recovery.
6. Multi-app workflows only after each component benchmark passes.

No calendar date should force progression between safety stages.
