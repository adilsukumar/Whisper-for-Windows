# Everyday-PC inference: hardware fit before downloads

## Observed machine and failure

2026-10-04: approximately 16 GiB physical RAM; Intel integrated graphics.
The portable 0.23.3 runtime detected CPU inference, not a supported GPU.
Llama 3.2 Vision loaded its architecture but required 10.7–10.9 GiB of
available memory. Normal applications left substantially less available.
Do not require users to close Chrome as a product prerequisite.
Do not confuse downloaded bytes, installed manifests, and successful inference.

## Implemented request budget

- Ollama requests explicitly use keep_alive=0 to unload after successful turns.
- Context 2048, batch 64, maximum generated response 512 tokens.
- This brain serializes text/vision inference; other Ollama clients are not controlled.
- Short context may truncate older conversation content. Long documents need future
  chunking/summarization, not a silently enlarged cache.
- These limits do not make the 11B model fit. Model weights remain large.
- STT, screenshots, application overhead and external programs still consume memory.
- Cancellation/failure unloading must be measured; keep_alive is not a hard process RAM cap.

## Installed candidate: image smoke test passed, product benchmark pending

Qwen3-VL 2B instruct Q4_K_M is listed at approximately 1.9 GB by Ollama:
https://ollama.com/library/qwen3-vl:2b-instruct-q4_K_M
The library specifies Ollama 0.12.7 or later. Prefer current D-backed 11435
for this candidate, not the legacy-runtime workaround for mllama.
Download size is NOT runtime memory. No guarantee of adequate latency or accuracy.
No model download is triggered by the new launcher or tests.

2026-10-05: the installed candidate passed the actual synthetic red-image
inference check on the current D-backed runtime, with normal applications left
open. It unloaded after the response. CPU latency and actual UI understanding
have not been accepted as product-quality; the acceptance procedure remains required.

## Acceptance procedure

1. Keep normal Chrome tabs and working applications open; record available RAM.
2. Before an authorized pull, verify 11435 is D-backed and has sufficient D: space.
3. Pull the exact candidate only once; preserve the installed Llama models.
4. Run from the repository:
   `.\.venv\Scripts\python.exe WhisperBrain\vision_readiness.py --model qwen3-vl:2b-instruct-q4_K_M --inference --ollama-url http://127.0.0.1:11435`
5. Require ready=true. Then repeat with synthetic small-text/button test images;
   a red-image pass is only a loading/input smoke test, not product-quality evidence.
6. Measure cold/warm response latency, peak memory, pagefile activity, and memory
   release after turns. If desktop responsiveness degrades, reject the profile.
7. Start `tools\Run-LowMemoryBrain.ps1` after installation and validation.
   The brain, Settings readiness and developer doctor now use this candidate
   by default for both text and vision, unless explicitly overridden.
8. Test text, consented vision, cancellation, and alternating requests together.

## Next architecture work (NOT implemented here)

### Local latency tuning — 2026-10-05

The desktop now sends an aspect-preserving preview with a maximum 640-pixel edge,
not a full-resolution monitor JPEG. Physical capture identity/bounds are retained;
local preview-to-monitor scale factors map model highlights back onto the display.
Small text may not be readable in this speed profile. The model is told not to
invent it. Screen consent remains required.

The brain retains the small model for ten idle minutes, uses the same 8192-token
cache for text and vision to avoid mode-switch reloads, and caps replies at 256
tokens with a concise-first instruction. This trades several GB of temporary idle
RAM for responsiveness; the model is no longer unloaded after every answer.

Measured adapter-only synthetic smoke tests on this CPU: cold two-token text
7.14 seconds, warm two-token text 0.86 seconds, 640×360 red-image question 36.80
seconds (first token 36.73 seconds), versus roughly 142 seconds at full HD.
These are not guaranteed UI/application timings or screenshot-reading quality
results. Vision remains too slow for spontaneous interaction on this CPU.
Accessibility-first routing is now implemented below; OCR remains future work.
There is no cloud fallback.

### Accessibility-first screen questions

With screen consent enabled, the desktop reads visible accessibility labels from
the app that was active when the shortcut was pressed. Reading is restricted to
the selected monitor, skips password/edit controls and their subtrees, and does
not read editable values. It is bounded to 160 nodes, 3000 characters and a
750-ms caller deadline. One background reader at a time prevents an unresponsive
provider from accumulating threads. No accessibility reading happens without
the question's screen consent.

When at least 80 characters are available, ordinary text-oriented questions use
the text model without image inference. Explicit visual questions (colors,
photos, charts, layout, highlights, etc.) and apps without enough accessible
text retain the image path. Accessibility labels are untrusted data, not model
instructions. The text path cannot judge appearance or place highlights. This
is app-text context, not a complete representation of everything on the monitor.

A dedicated WPF fixture verified actual label reading in 161 ms and exclusion
of editable/password values. Synthetic screen-label inference took 1.64 seconds
and 0.62 seconds on the subsequent warm request. Real app accessibility support
and UI-to-answer latency still need user testing. A 320×180 image experiment
still took 25.81 seconds and was not enabled: losing more image detail alone is
not an adequate solution. The UI distinguishes screen-text reading from slower
image analysis. No OCR implementation or near-instant visual interpretation is
claimed.

The running brain's authenticated WebSocket test subsequently confirmed that
screen-text context bypasses image inference: 25.69 seconds after a cold model
load, then 4.02 seconds warm. Thus subsecond adapter tests are not a guarantee
of app-to-answer latency. Idle retention was increased from two to ten minutes
to reduce repeated cold starts, at the expense of keeping model RAM occupied.

Prefer consented Windows UI Automation and OCR context before expensive vision.
Accessibility/OCR results and web/app content are untrusted data, not instructions.
Maintain original coordinate transforms for cropped/resized captures.
Never capture private windows or upload screen content implicitly.
Optional hosted vision requires explicit consent and a budget; local failure must
never silently switch to cloud. Pin reviewed model licenses and notices for launch.
