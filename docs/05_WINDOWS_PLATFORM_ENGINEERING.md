# Windows Platform Engineering

## Objective

Turn Windows-specific complexity into a product advantage. This document defines the display, capture, automation, input, audio, lifecycle, and privilege behavior required for a trustworthy native application.

## DPI and coordinate contract

### Required coordinate spaces

- **Desktop physical pixels:** Win32 cursor, UIA bounds, and monitor rectangles.
- **WPF DIPs:** layout within each overlay window.
- **Capture pixels:** coordinates within an image after crop/scale/encode.
- **Normalized coordinates:** optional `[0,1]` representation for model exchange.

Every coordinate-bearing object follows the required-fields-by-space table in `19_DATA_AND_PROTOCOL_SCHEMAS.md`. Use `display_id` consistently; never pass anonymous `x/y` values between modules.

### Implementation steps

1. Declare Per-Monitor DPI Awareness V2 before any window is created.
2. Enumerate monitors using display configuration APIs, preserving negative coordinates.
3. Create a `CoordinateTransformService` with pure conversion methods.
4. Store each capture's source rectangle and output dimensions.
5. Convert annotations from capture pixels → desktop pixels → overlay DIPs.
6. Recompute on `WM_DPICHANGED`, display attach/detach, rotation, RDP change, or primary-display change.
7. Invalidate stale annotations after topology changes.
8. Add parameterized tests across the monitor/DPI matrix.

## Overlay topology

- Create one non-activating, click-through annotation window per monitor.
- Keep the companion on the monitor containing the cursor.
- Create text input, confirmation, settings, and history as separate focusable windows.
- Use `WS_EX_NOACTIVATE` where appropriate so annotations do not steal focus.
- Ensure the overlay is excluded from capture when supported; otherwise hide it for the capture frame.
- Clamp bubbles to the work area and avoid the taskbar.
- Respect fullscreen applications, Focus Assist, games, screen sharing, and reduced-motion settings.

## Screen capture

### Preferred strategy

- Use Windows Graphics Capture for individual windows/displays when available.
- Use a controlled desktop capture fallback for scenarios WGC cannot cover.
- Capture only the minimum region required by the task.
- Avoid continuous capture until a feature has explicit disclosure and consent.

### Capture metadata

Record capture ID, source kind, HWND/display ID, source bounds, encoded dimensions, scale factor, timestamp, active/focused state, and redaction regions.

### Failure handling

- Protected/black frames: report that content could not be captured.
- Minimized windows: offer to restore or use UIA context.
- Permission/policy denial: show the exact Windows setting or enterprise policy involved.
- Display disconnected during capture: cancel and reacquire context.

## UI Automation

### Snapshot contents

- Stable per-snapshot element ID and runtime identity.
- Control type, name, automation ID, value summary, state, enabled/offscreen status, bounds, and supported patterns.
- Focused element, ancestors, relevant siblings, and actionable descendants.
- Strict node/depth/character limits.

### Targeting order

1. Exact automation ID/name with expected control type.
2. Scoped semantic search within the active window or known container.
3. UIA element at/near the cursor.
4. OCR text matched to UIA bounds.
5. Vision region proposal verified against visible layout.
6. Ask the user when confidence remains below threshold.

### Execution preference

Use `Invoke`, `Value`, `Selection`, `ExpandCollapse`, `Toggle`, `Scroll`, and related UIA patterns. Only inject mouse/keyboard input when semantic patterns are unavailable and the window is foregrounded.

## Input and hotkeys

- Prefer `RegisterHotKey` for simple global shortcuts; reserve low-level hooks for cases it cannot handle.
- Allow customization and validate conflicts.
- Do not log raw keystrokes.
- Scope injected input to an expected foreground window and verify focus immediately before use.
- Abort if focus changes unexpectedly.
- Offer a visible panic/cancel shortcut that cannot be overridden by an agent.

## UAC and integrity levels

- Never attempt to automate or dismiss the secure desktop.
- Default client and executor run at medium integrity.
- If an approved workflow genuinely requires elevation, launch a narrowly scoped signed helper with an explicit Windows consent prompt.
- Validate commands across the elevation boundary using an allowlisted protocol.
- Never run the entire assistant permanently elevated.
- Explain that elevated applications may be unavailable to a medium-integrity assistant.

## Audio and media sessions

- Enumerate capture/render devices and respond to device changes.
- Handle exclusive-mode conflicts, Bluetooth profile switching, unplug/replug, and communications-device changes.
- Keep capture/playback off the UI thread.
- Duck or pause TTS when the user starts speaking.
- Release audio devices promptly on shutdown or mode changes.

## Lifecycle

- Retain the single-instance mutex for application lifetime.
- Use a tray icon with Open, Pause Listening, Privacy Mode, Diagnostics, Check for Updates, and Quit.
- Handle startup registration transparently and make it reversible.
- Persist only necessary state atomically with versioned migrations.
- Recover after sleep/resume, Fast User Switching, RDP reconnect, and explorer restart.

## Compatibility test matrix

- Windows 10 and Windows 11 minimum builds selected by policy.
- x64 initially; ARM64 only after dependencies and CI are available.
- One, two, and three monitors.
- 100/125/150/175/200% scaling, including mixed combinations.
- Monitors positioned left, right, above, and below primary.
- Portrait rotation, HDR, docking, display hot-plug, RDP, and virtual desktops.
- WPF, Win32, WinUI, Electron, browser, Java, Qt, and game/fullscreen surfaces.
