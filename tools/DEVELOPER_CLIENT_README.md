# Whisper Windows 11 x64 developer client

This is an unsigned engineering build, not an installer or public release.
Check `manifest.json`: when `self_contained_dotnet` is true the client includes its
.NET runtime; when false it requires the .NET 8 Windows Desktop Runtime installed
separately. It does not include Python, the
local brain, Ollama, model files, account hosting, or billing enforcement.

1. Extract the entire folder; do not run inside the ZIP.
2. In the development repository, install the pinned brain dependencies and
   start `dev.ps1 run-brain`. Confirm Ollama and required models are ready.
3. Run `client/Whisper.exe` as your ordinary Windows user, not administrator.
4. Find Whisper in the tray. Ask opens text input; Settings controls screen
   consent, spoken responses, and reduced motion. Pause stops local capture.
5. Quit through the tray before deleting or replacing this developer folder.

No automatic startup registration, background updater, or uninstall operation
is included. User preferences/authentication are stored separately under your
Windows local application data. Model storage follows your Ollama configuration.
Do not disable Windows security protections to run an unsigned build.

`manifest.json` records file SHA-256 values. The accompanying ZIP checksum detects
accidental corruption, not malicious replacement: neither is digitally signed.
Do not distribute publicly before code signing, third-party notices, clean-machine
installation tests, privacy/security review, and commercial enforcement are ready.
