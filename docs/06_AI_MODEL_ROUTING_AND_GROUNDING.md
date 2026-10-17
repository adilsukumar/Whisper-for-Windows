# AI, Model Routing, and Grounding

## Design goals

- Use the smallest/fastest capable path.
- Separate intent, reasoning, grounding, and execution.
- Allow local, cloud, and hybrid operation.
- Make model quality measurable and replaceable.
- Never grant model output direct authority to execute.

## Capability router

The router receives intent text, available context, privacy mode, hardware profile, provider health, latency budget, and conversation state. It returns a pipeline plan and fallback chain.

### Intent classes

- Dictation/transformation.
- General knowledge.
- Screen explanation.
- UI element search/pointing.
- Multi-step tutoring.
- Action request.
- File/workspace task.
- Settings/help/diagnostics.
- Unsafe or unsupported request.

### Routing policy

1. Deterministic command handling before LLM routing.
2. UIA search before vision for named controls.
3. Cursor-centered crop before full-screen vision where sufficient.
4. Small text model for classification and concise general answers.
5. Vision-language model only when visual content matters.
6. Stronger cloud model only when allowed and local confidence/latency is inadequate.
7. No model call for direct local settings, diagnostics, or known commands.

## Hardware profiler

Collect with consent: OS build, architecture, CPU, RAM, GPU vendor/model, dedicated/shared VRAM, supported acceleration, free disk, and a short benchmark.

Profiles:

- **Basic:** local STT/router/TTS; cloud reasoning/vision.
- **Capable:** local small text and compact vision models; cloud fallback.
- **Private workstation:** all-local pipeline with larger models.
- **Managed enterprise:** administrator-selected endpoints and policies.

Never market “fully local” without publishing concrete RAM/VRAM/disk and expected latency requirements.

## Model adapter contract

Each adapter reports:

- Supported modalities and tool/schema capabilities.
- Context/output limits.
- Expected license and artifact identity.
- Health, warm/cold state, and estimated latency.
- Token/image/audio usage where applicable.
- Cancellation support and failure classification.

Adapters must not leak provider-specific message formats into product logic.

## Grounding pipeline

There are three ordered paths:

1. **Deterministic UIA path:** semantic search/resolution produces candidates and confidence from exact identity/name/type/state evidence; no model is required.
2. **OCR-assisted path:** OCR text regions are matched to UIA/layout and validated; a model is optional only for ambiguous language.
3. **Model-assisted vision path:** a vision model proposes candidates, but the resolver validates them against current capture/UI state.

The resolver—not the model—owns the final confidence used by annotation/action policy. Failure in one path may fall through to the next only when privacy mode permits it.

### Step 1: collect

Collect the active window, cursor neighborhood, relevant UIA subtree, screenshot/crop, OCR blocks, display/capture transform, and conversation references.

### Step 2: propose

The selected path returns structured references such as element description, candidate semantic IDs, normalized region, intended annotation/action, and evidence—not markdown-embedded JSON. Only the model-assisted path involves a model.

### Step 3: resolve

Match the proposal against current UIA elements/OCR/vision regions. Prefer stable semantic identity. Reacquire the element if the UI changed.

### Step 4: validate

- Bounds lie within the referenced capture/display.
- Target is visible and enabled where required.
- Label/role/state are compatible with the requested action.
- Confidence exceeds the policy threshold.
- Capture/element is not stale.

### Step 5: annotate or ask

At high confidence, annotate. At medium confidence, show alternatives or ask the user. At low confidence, explain that the element could not be located.

## Context construction

- Separate system policy from screen-derived text.
- Delimit untrusted screen/OCR content explicitly.
- Use app profiles as concise capability hints, not inflated personality prompts.
- Include only relevant UIA subtrees and image crops.
- Summarize conversation history into typed facts, unresolved tasks, and user preferences.
- Do not retain screenshots by default.

## Prompt-injection controls

- Text seen on screen cannot override system policy, select tools, modify approval rules, or authorize external effects.
- Treat phrases such as “ignore prior instructions” as visible content unless the user explicitly asks to analyze them.
- Action proposals must be justified by the user's current request, not merely by screen text.
- High-risk actions require local confirmation regardless of model confidence.

## Evaluation sets

### Router benchmark

At least 500 labeled utterances across intent classes, ambiguous phrasing, multilingual requests, and context-dependent follow-ups. Track accuracy, unnecessary vision calls, fallback rate, and latency.

### Grounding benchmark

For each supported application, store approved screenshots/UIA snapshots and labeled target elements across themes, scaling, window sizes, and versions. Track top-1 target accuracy, IoU for non-UIA regions, false-confident points, wrong-monitor/out-of-bounds rate, ambiguity deferral, confidence calibration, and time to target. Release gates apply per supported app/version/DPI stratum. Wrong-monitor/out-of-bounds must be zero; the false-confident threshold must be chosen from user-risk analysis before Alpha and may not be hidden by an aggregate score.

### Response benchmark

Evaluate correctness, brevity, instruction order, grounded references, uncertainty, and refusal quality. Use blind comparisons across candidate models.

## Model release checklist

- Exact model/version/digest recorded.
- License reviewed for use and redistribution.
- Safety and prompt-injection tests pass.
- Quality and latency benchmarked on each hardware tier.
- Disk/download/VRAM requirements documented.
- Fallback and uninstall behavior tested.
- Model update can roll back independently of the application.
