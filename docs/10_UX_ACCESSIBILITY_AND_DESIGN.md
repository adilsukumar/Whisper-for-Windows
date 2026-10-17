# UX, Accessibility, and Design System

## Experience principles

- Present at the moment of need and disappear cleanly.
- Show system state honestly.
- Prefer one useful next step over a wall of text.
- Never make the companion obscure the interface it is explaining.
- Make every action and capture visible and stoppable; disclose when committed effects cannot be reversed.
- Personality is warm and concise; it never delays work.

## Interaction states

| State | Visual treatment | User controls |
|---|---|---|
| Idle | Small, calm companion or tray-only mode | Invoke, hide, move |
| Listening | Clear microphone indicator and live level | Release/stop/cancel |
| Transcribing | Partial transcript | Edit/cancel |
| Thinking | Subtle progress plus task summary | Cancel |
| Pointing | Ordered annotation and concise explanation | Next/back/repeat/dismiss |
| Speaking | Speech-synced state | Pause/stop/barge in |
| Awaiting confirmation | Focused preview panel | Explicit action/cancel/edit |
| Acting | Current step and progress | Stop immediately |
| Recovering/Error | Plain-language cause and next action | Retry/diagnostics/fallback |

## Window model

- Annotation overlays remain click-through and non-activating.
- Companion can be hidden, pinned, or replaced by a minimal status dot.
- Text entry uses a compact focused window rather than activating a full-screen overlay.
- Long responses open in a resizable history panel; the cursor bubble stays concise.
- Confirmations are modal only to Whisper's action flow, not to the entire desktop.

## Annotation vocabulary

- **Pulse:** single location.
- **Box:** target control/region.
- **Arrow/path:** relationship or motion.
- **Step marker:** ordered workflow.
- **Spotlight:** dim non-target regions for teaching.
- **Caption:** short anchored explanation.

Annotations must avoid sensitive text, remain within monitor bounds, meet contrast requirements, and be dismissible. They expire when the underlying capture becomes stale.

## Onboarding

1. Explain capture behavior and privacy in one screen.
2. Select/test microphone and output voice.
3. Choose/record hotkey and resolve conflicts.
4. Run hardware benchmark and recommend Local Only, Automatic, or Cloud Allowed.
5. Download only required components with size/time estimates.
6. Ask a guided question in a safe sample UI.
7. Verify the assistant pointed correctly and played audio.
8. Offer startup behavior, exclusions, and optional diagnostics sharing.

The onboarding must be resumable and provide a diagnostics path when any step fails.

## Settings information architecture

- General: startup, language, updates, theme.
- Voice: input/output devices, voice, rate, test.
- Hotkeys: talk, text, cancel, hide.
- Intelligence: Local Only, Automatic, Cloud Allowed, model downloads, providers.
- Screen and Privacy: capture scope, excluded apps, retention, telemetry.
- Actions: permission levels, stricter optional confirmations, and audit history. Settings cannot weaken the minimum policy in `08_COMPUTER_USE_AND_SAFETY.md`.
- Companion: pack, size, offset, animation, reduced motion.
- Accessibility: keyboard navigation, contrast, captions, speech rate.
- Diagnostics: dependency health, logs, export, reset.

## Accessibility

- Full keyboard navigation and visible focus.
- Screen-reader names/descriptions for every control.
- Captions/transcripts for all speech.
- Do not rely on color alone for state.
- High-contrast and reduced-motion support.
- Adjustable font, bubble width, display duration, and TTS rate.
- Optional dwell/voice activation considered only after privacy testing.
- Avoid hotkeys that conflict with common assistive software.

## Companion packs

Companion visuals can support retention and monetization without affecting trust:

- Core neutral companion ships free.
- Cosmetic packs may change shape, expressions, and sounds.
- Packs cannot change safety indicators or confirmation behavior.
- Assets are versioned, performance-budgeted, and accessible.
- No dark patterns, randomized purchases, or paywalling essential accessibility.

## UX validation

- Five-user usability tests for onboarding before private beta.
- Task-based tests for explain, point, act, cancel, and recover.
- Measure time-to-first-success and abandonment per step.
- Record confusion around local/cloud privacy labels.
- Validate text/input windows across monitor edges and scaling.
