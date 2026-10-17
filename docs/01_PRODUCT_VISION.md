# Product Vision and Strategy

## Vision

Whisper makes complex Windows software understandable and operable through natural conversation. It should feel like a patient expert who sees the same screen, understands the user's intent, points precisely, and can take carefully controlled action.

## Problem

Desktop software remains difficult because users must translate a goal into application-specific menus, controls, terminology, and sequences. Generic chat assistants can explain concepts but lack trustworthy screen grounding. Macro tools can act but do not understand intent. Existing cursor companions demonstrate compelling interaction but often require API keys, have inconsistent Windows behavior, or rely on visual guessing.

## Target users

### Initial beachhead

- Students and self-learners using unfamiliar professional software.
- Creators learning tools such as DaVinci Resolve, Blender, Photoshop alternatives, and audio editors.
- Knowledge workers learning Excel, PowerPoint, enterprise portals, and internal tools.
- Developers navigating IDEs, terminals, debuggers, and cloud consoles.

### Later segments

- Accessibility users who benefit from voice-first navigation.
- Support teams that need guided troubleshooting and repeatable workflows.
- Small businesses seeking private desktop automation.
- Enterprises requiring managed inference, audit logs, and policy controls.

## Jobs to be done

1. “Explain what I am looking at without making me describe the application.”
2. “Show me exactly where the control is.”
3. “Teach me why this workflow works, not only which button to press.”
4. “Perform this repetitive task while showing what will happen.”
5. “Remember how I prefer to do this next time.”
6. “Keep sensitive content on my computer when I choose Local Only.”

## Positioning

**For Windows users learning or operating complex desktop software, Whisper is a native screen-aware assistant that combines voice, semantic UI understanding, precise visual guidance, and safe actions. Unlike screenshot-only companions, Whisper grounds answers in Windows UI Automation and verifies the result.**

## Principles

- Reliability beats breadth.
- Semantic targeting beats raw coordinates.
- Fast partial feedback beats silent waiting.
- The user remains in control.
- Privacy must be understandable, not buried in legal text.
- Local and cloud inference are delivery modes, not ideologies.
- Personality supports utility; it must never obstruct the task.
- Every action should be observable, attributable, and stoppable; effects committed before a stop may not be reversible.

## Differentiation

### Required moat

- First-class multi-monitor and mixed-DPI behavior.
- UI Automation grounding with vision and OCR fallback.
- A benchmarked action layer with post-action verification.
- Automatic hardware-aware local/cloud routing.
- App-specific guidance packs built from real workflows and evaluations.
- A safe, signed, low-friction Windows distribution.

### Not a moat by itself

- WPF instead of Electron.
- A cursor-following animation.
- Connecting to Ollama.
- A large system prompt per application.
- Supporting many model providers.

## Scope boundaries

These are product-scope summaries, not release gates. Canonical release naming and gates are defined in `14_EXECUTION_ROADMAP.md`.

### MVP

- Push-to-talk and text input.
- Explicit screen capture only while invoked.
- Active-window and cursor context.
- STT, streaming response, TTS, and interruption.
- Accurate on-screen pointing on supported applications.
- One-click installer and automatic configuration.
- Local Only, Automatic, and Cloud Allowed processing modes.

### Beta

- Multi-monitor/mixed-DPI completeness.
- Semantic actions: invoke, set value, select, scroll, and open.
- Confirmation policy and audit history.
- App-specific tutor packs and workflow replay.
- Payments and usage controls.

### After 1.0

- Extensible skills SDK.
- Team workflow libraries.
- Remote managed inference.
- Long-running background agents.
- Optional personal knowledge and memory.

## Product risks

| Risk | Consequence | Mitigation |
|---|---|---|
| Official competitors ship Windows support | Distribution advantage disappears | Win through native grounding, safety, and focused app expertise |
| Local models are slow on ordinary PCs | Poor first impression | Hardware benchmark and Automatic-mode cloud fallback when permitted |
| Vision points at the wrong control | Trust collapses | UIA-first grounding, confidence thresholds, verification |
| Automation causes damage | Legal and trust risk | Policy tiers, confirmations, dry runs, audit and rollback where possible |
| Screen content contains prompt injection | Agent hijack | Treat screen as data, isolate instructions, allowlisted tools, confirmations |
| Setup is complicated | Activation failure | Signed bootstrapper, managed dependencies, health diagnostics |
| Product becomes a feature bundle without a use case | Low retention | Launch around guided mastery of complex software |

## North-star outcome

A new user installs Whisper, presses one hotkey, asks a real question about an unfamiliar application, receives a useful spoken answer with the correct element highlighted, and returns voluntarily within seven days.
