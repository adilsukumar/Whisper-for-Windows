# Keyboard controls

| Shortcut | Action |
| --- | --- |
| Ctrl + Shift + H | Open the controls/help card |
| Ctrl + Shift + Q | Exit the Whisper desktop app, including its tray icon |
| Ctrl + Shift + Space | Open typed input |
| Ctrl + Space (hold) | Record speech; release to submit |
| Escape | Stop/dismiss the current interaction |
| Enter (in typed input) | Submit the question |

Help and quit use Windows-registered, non-repeating global hotkeys and remain
available while Whisper is paused. They are unregistered on exit. If another
app already owns either shortcut, the help card reports that conflict; use the
tray Controls / Help or Quit option instead. Whisper does not steal that binding.

Help is a single-instance, rounded card placed on the cursor's monitor. Opening
it dismisses the current interaction. Quitting releases desktop resources and
removes the tray icon. It does not terminate the separately running Python brain
service or Ollama, and does not delete anything.

## Reopening Whisper

Double-click `Start-Whisper.cmd` in the project root. The launcher starts the
brain if needed, or reuses a verified running project brain on port 8765. It
sets the bundled .NET runtime and shared local authentication-token path before
opening the client. It never prints the token, disables authentication or
restarts Ollama. If Whisper is already open, quit it first with Ctrl+Shift+Q.
