# Voice and Realtime Pipeline

## Experience target

The user holds a hotkey, speaks naturally, releases, sees the final transcript almost immediately, receives useful partial output quickly, hears a natural response, and can interrupt at any time.

## End-to-end flow

```text
Hotkey down → audio pre-roll → streaming capture → VAD/partial STT
→ hotkey up/end-of-speech → final transcript → route/context
→ streamed model response → sentence chunker → streamed TTS/playback
→ barge-in cancels model and audio → next turn
```

## Audio capture

- Prefer WASAPI capture with device selection and device-change handling.
- Use 16 kHz mono PCM for standard speech models unless a provider requires another format.
- Stream bounded audio frames rather than one base64 WAV after recording.
- Maintain a short pre-roll so the first phoneme is not clipped.
- Display an input-level meter and detect silence/disconnected devices.
- Cap maximum utterance length and provide a visible continuation option.

## Voice activity detection

- Hotkey is authoritative for push-to-talk.
- VAD helps trim silence, create partial chunks, and warn when no speech is detected.
- Optional hands-free mode requires a wake phrase, prominent listening indicator, and separate privacy review.

## STT providers

Define one interface for local and cloud engines:

- Start session.
- Accept audio frames.
- Emit partial transcript.
- Finalize and emit final transcript, language, confidence, and timing.
- Cancel.

Local candidates may include whisper.cpp, faster-whisper, or platform-native engines. Select after benchmarking accuracy, startup time, CPU/GPU usage, distribution size, and license. Do not make runtime `pip install` part of the product.

## Transcript UX

- Show partial text near the companion without covering the target UI.
- Let the user edit the final transcript before consequential actions.
- Preserve application names and technical terms through context vocabulary.
- Support automatic language detection plus an explicit language setting.
- Mark low-confidence words when they materially affect an action.

## Response streaming

- Orchestrator emits typed response deltas.
- UI displays deltas at a readable cadence.
- TTS starts after a stable clause/sentence, not after the full answer.
- Structured annotations/actions are sent separately from spoken prose.
- Cancellation propagates through model generation, TTS synthesis, and playback.

## TTS

Provide tiers:

- Built-in/offline Windows voice as the reliable zero-setup fallback.
- Higher-quality local neural voice where hardware permits.
- Optional cloud premium voices.

Requirements:

- Select voice, speed, volume, and automatic language voice.
- Cache only safe reusable synthesis fragments, not private responses.
- Strip markdown/code formatting from speech while preserving the visual response.
- Handle code, URLs, filenames, and abbreviations intelligibly.
- Synchronize companion animation with actual playback amplitude/state.

## Barge-in and cancellation

- Hotkey down while TTS is playing immediately ducks/stops playback.
- Increment request generation so late deltas are ignored.
- Cancel downstream work where supported.
- Never execute an action after its parent request was cancelled.

## Latency budgets

| Stage | p50 target | p95 target |
|---|---:|---:|
| Hotkey release → final transcript visible | 500 ms | 1.5 s |
| Route decision | 50 ms | 150 ms |
| Context collection for normal request | 150 ms | 500 ms |
| Text first token after transcript | 1.0 s | 3.0 s |
| Vision first token after transcript | 3.0 s | 8.0 s |
| Stable sentence → audible TTS start | 400 ms | 1.2 s |

Targets are product goals, not claims. Measure on each published hardware/network tier.

## Voice tests

- Quiet/noisy rooms, accents, soft/loud speech, short/long utterances.
- Bluetooth headset switching and device removal.
- No speech, clipped speech, network loss, model cold start.
- Interrupt at every pipeline stage.
- Technical vocabulary in each launch application.
- Verify raw audio is not retained or logged by default.
