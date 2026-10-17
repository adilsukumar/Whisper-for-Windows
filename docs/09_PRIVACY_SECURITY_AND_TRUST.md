# Privacy, Security, and Trust

## Trust promise

- Whisper captures the screen/microphone only when the user intentionally invokes it, except for separately enabled modes with persistent indicators.
- Screenshots and audio are not retained by default.
- The UI states clearly when local or cloud processing is used.
- The user can pause, inspect, export, and delete stored data.
- Whisper never bypasses Windows security controls.

## Assets to protect

- Screen and OCR content.
- Audio and transcripts.
- Files and clipboard data.
- Provider keys and account tokens.
- Conversation history and preferences.
- Local action authority.
- Update channel and distributed binaries.

## Threat actors and scenarios

- Malicious content on screen attempting prompt injection.
- Another local process connecting to the control endpoint.
- Compromised model/provider producing unsafe actions.
- Dependency or update supply-chain compromise.
- Accidental capture of passwords, banking, private messages, or notifications.
- Logs/crash dumps containing secrets.
- Plugin/skill requesting excessive privileges.

## Security architecture

### Local transport

- Bind to loopback or use named pipes with access control.
- Require a per-install authentication secret.
- Authenticate every new connection and rotate secrets safely.
- Apply message size, rate, concurrency, and time limits.
- Reject unknown protocol versions and fields in security-sensitive schemas.

### Secret storage

- Use Windows DPAPI/Credential Manager or an appropriate protected vault.
- Never store keys in `.env` for production users.
- Never place secrets in command-line arguments, logs, crash reports, or model prompts unnecessarily.

### Process isolation

- Run the UI, orchestrator, updater, and any elevated helper with the least privilege possible.
- Sandbox third-party plugins/skills and give them explicit capabilities.
- Treat model files as untrusted large artifacts; validate hashes and sources.

## Privacy controls

- Capture scope: window, monitor, region, or all displays.
- Processing mode: Local Only / Automatic / Cloud Allowed.
- Application exclusion list and temporary pause.
- Browser private-window and password-manager detection where technically reliable.
- Notification redaction option.
- Retention duration for transcripts, summaries, action history, and diagnostics.
- Per-request “do not remember” control.

## Normative processing modes

Use only these names in product/docs: **Local Only**, **Automatic**, and **Cloud Allowed**. “Hybrid” is the architecture behind Automatic, not a separate user-facing mode. A fallback may never cross from Local Only to cloud silently.

| Data/process | Local Only | Automatic | Cloud Allowed |
|---|---|---|---|
| Raw microphone audio | Local STT; not uploaded | Local STT by default; cloud STT only if selected/disclosed | May use selected cloud STT with active indicator |
| Transcript/request text | Local models only | Cloud text/vision permitted when router predicts material benefit; per-request cloud indicator | May use configured providers |
| Full screenshot | Local only | Prefer UIA/crop; cloud only after capture filtering and visible indication | May send selected capture scope after filtering |
| Cropped image/OCR/UIA | Local only | Send minimum necessary subset when cloud route is selected | May send required subset to configured provider |
| TTS text | Local TTS | Local by default; cloud premium voice only if enabled | Selected local/cloud voice |
| Operational telemetry | Optional, content-free network traffic disclosed separately | Same | Same |
| Updates/model downloads | Network allowed unless user disables checks; never include screen/user content | Same | Same |
| Failure | Explain missing local capability; offer an explicit mode change | Fall back among permitted local/cloud adapters and show resulting mode | Fall back among configured providers; never broaden capture scope |

The request UI/log records which processors received which data categories. Account, telemetry, update, license, and diagnostics traffic are disclosed independently because inference mode does not imply the machine is entirely offline.

## Data inventory and default retention

| Artifact | Default | Storage/protection | Cloud behavior | User control/deletion |
|---|---|---|---|---|
| Raw audio | Memory only; discard after turn | Process memory | Provider retention governed by selected service and disclosed before use | Local deletion automatic; provider policy linked |
| Screenshots/crops | Memory only; discard after turn | Process memory | Minimum selected context; provider policy applies | Capture preview/scope; no local history by default |
| Transcript/response | Current session only by default | Versioned local store if history enabled; encrypt sensitive persisted data | Provider policy applies to submitted content | Per-turn do-not-remember; clear/export/delete |
| Conversation summary/preferences | Opt-in/minimal | Local per-user application data | Send only when needed and permitted | Inspect/edit/delete; uninstall choice |
| Action audit | Local bounded retention; proposed 30 days pending product decision | Protected local app data with redaction | Not uploaded by default | View/export/delete; security-critical entries may have documented minimum retention in managed mode |
| Operational logs | Rotating 7-day default proposed | Local app data, content redaction | Uploaded only with explicit diagnostics consent | Preview/export/delete |
| Crash dump | Off or local-minimal by default | Restricted local crash directory | Explicit upload consent after preview | Delete from diagnostics/settings/uninstall |
| Secrets | Until revoked/uninstall choice | DPAPI/Credential Manager | Sent only to the intended provider endpoint | Revoke/replace/delete; never export in diagnostics |
| Billing/account | Required while account/legal obligations exist | Service database under published policy | Cloud service | Account deletion subject to legal records |

Final retention periods, provider policies, backup deletion timing, and managed-enterprise exceptions must be decided before private beta and reflected in the privacy policy.

## Sensitive-surface filtering

Signals may include process allow/deny lists, window titles, UIA password properties, known credential controls, OCR patterns, and user-defined rules. Detection is defense-in-depth, not a guarantee. If uncertain, prefer withholding capture and ask the user.

## Prompt injection defense

- Keep user instructions, policy, and screen-derived content in separate structured fields.
- Models cannot change policy or confirmation tier.
- Screen text alone cannot authorize a tool call.
- Tool arguments are validated against current user intent and application scope.
- Consequential effects require local confirmation independent of model claims.

## Supply chain

- Pin NuGet/Python/native/model dependencies and record hashes.
- Generate an SBOM for releases.
- Scan source and dependencies in CI.
- Sign binaries, installer, and update metadata.
- Verify update signatures before installation.
- Maintain third-party notices and per-model license records.
- Remove bundled installers from source control; fetch from pinned official sources.

## Telemetry policy

Default telemetry should contain operational events such as startup success, feature state, durations, error codes, hardware tier, and anonymous funnel steps. It should not contain screenshots, audio, transcript text, file paths, window titles, typed content, or provider keys. Product analytics must be documented and controllable.

## Security review gates

### Before private beta

- Threat model reviewed.
- Authenticated local transport.
- Protected secrets.
- Redacted logs.
- Signed installer.
- Capture indicator and exclusion settings.

### Before action beta

- Tool allowlist and schema validation.
- Prompt-injection test suite.
- Confirmation policy tests.
- Audit log.
- Independent review of input injection and elevated helper design.

### Before 1.0

- Dependency/SBOM automation.
- Update signing and rollback.
- Vulnerability intake and response policy.
- External security assessment proportional to product risk.
- Published privacy policy and data-flow disclosure.

## Incident response

1. Disable affected provider/feature using a signed configuration update if possible.
2. Preserve minimal diagnostic evidence without expanding data collection.
3. Identify impacted versions/users.
4. Patch, test, sign, and release.
5. Notify users clearly when their data or action authority may have been exposed.
6. Document cause, correction, and prevention.
