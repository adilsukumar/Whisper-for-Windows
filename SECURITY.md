# Security Policy

Whisper is pre-release software and is not yet suitable for sensitive or autonomous production workflows.

## Supported versions

No public production version is currently supported. This section must be updated before distribution.

## Reporting a vulnerability

Before public release, configure a monitored private security contact and replace this paragraph with the address and response expectations. Do not request vulnerability details through public issues.

## Security boundaries

- Models are untrusted and may only propose typed actions.
- The Windows client owns approval and execution.
- Whisper does not bypass UAC or the secure desktop.
- Screen/OCR content is untrusted and cannot alter policy.
- Secrets must use Windows-protected storage in production.
- Production endpoints must authenticate local clients.

## Known pre-release limitations

- Current local WebSocket transport is unauthenticated.
- Production secret storage is not implemented.
- Action execution and policy enforcement are not implemented.
- Prompt-injection and security regression suites are not implemented.
- Installer/update signing is not implemented.

See `docs/09_PRIVACY_SECURITY_AND_TRUST.md` for the target threat model and security gates.
