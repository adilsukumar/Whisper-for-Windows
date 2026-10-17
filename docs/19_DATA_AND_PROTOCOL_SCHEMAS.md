# Data and Protocol Schemas

This document specifies conceptual schemas. Convert them into shared generated types and versioned JSON Schema/Protocol Buffers before implementation.

## Envelope

```json
{
  "protocol_version": "1.0",
  "event_type": "request.start",
  "session_id": "uuid",
  "request_id": "uuid",
  "sequence": 1,
  "timestamp": "2026-09-24T10:00:00Z",
  "payload": {}
}
```

Rules:

- `request_id` is unique per turn; `session_id` persists across turns.
- Sequence is monotonic within a request.
- Unknown major versions are rejected; compatible minor fields may be ignored.
- Size limits apply per event type.
- Errors use codes, retryability, safe user message, and internal diagnostics ID.

## Request start

Fields: input modality, transcript/text, requested capture scope, privacy mode, locale, active app reference, cursor reference, and capability hints. Audio frames travel as bounded binary messages or a dedicated stream rather than base64 in a giant JSON request.

## Normative coordinate primitives

Coordinates use a top-left origin. Rectangles are half-open: left/top are included and right/bottom are excluded. Width/height are non-negative. Desktop physical pixels may be negative because monitors can be left/above the primary display.

```json
{
  "point": {
    "space": "desktop_px",
    "x": -120,
    "y": 450,
    "display_id": "display-2",
    "capture_id": null,
    "reference_width": null,
    "reference_height": null,
    "transform_version": 7
  },
  "rect": {
    "space": "capture_px",
    "x": 10,
    "y": 20,
    "width": 100,
    "height": 40,
    "display_id": "display-2",
    "capture_id": "uuid",
    "reference_width": 1920,
    "reference_height": 1080,
    "transform_version": 7
  }
}
```

Allowed spaces: `desktop_px`, `overlay_dip`, `capture_px`, and `normalized`. Normalized values are in `[0,1]` relative to the stated reference dimensions. Conversion to integer pixels rounds outward for rectangles (floor left/top, ceil right/bottom) and nearest-even for points. Rotated displays are represented in desktop orientation after OS rotation; raw-sensor coordinates are never exposed.

Required fields by space:

| Space | `display_id` | `capture_id` | reference dimensions | transform version |
|---|---|---|---|---|
| `desktop_px` | Required | Null unless derived from a capture | Optional/null | Required |
| `overlay_dip` | Required | Null unless derived from a capture | Optional/null | Required |
| `capture_px` | Required target display | Required | Required encoded capture width/height | Required |
| `normalized` | Required target display | Required when normalized to a capture | Required normalization width/height | Required |

The normalized point schema uses the same point fields with non-null `reference_width` and `reference_height`. `display_id` is the only monitor identifier name used across schemas.

`CaptureTransform` records source desktop rectangle, output dimensions, crop origin, resize scale, rotation already applied, contributing display IDs, and transform version. A capture spanning displays is a desktop capture with one source rectangle and an explicit list of intersecting displays; an annotation still identifies the target display after conversion.

## Context bundle

```json
{
  "capture_id": "uuid",
  "captured_at": "ISO-8601",
  "active_window": {
    "window_id": "opaque",
    "process_name": "example",
    "title_redacted": true,
    "bounds": {"space": "desktop_px", "x": 0, "y": 0, "width": 1000, "height": 800, "display_id": "display-1", "capture_id": null, "reference_width": 1000, "reference_height": 800, "transform_version": 7}
  },
  "cursor": {"space": "desktop_px", "x": 500, "y": 400, "display_id": "display-1", "capture_id": null, "transform_version": 7},
  "displays": [],
  "images": [],
  "uia": {"snapshot_id": "uuid", "elements": []},
  "ocr": [],
  "redactions": []
}
```

## UI element reference

Include snapshot ID, opaque element ID/runtime identity, control type, semantic name hash/summary, bounds, supported patterns, and freshness timestamp. Never trust an old reference without reacquiring and validating it.

## Grounding result

```json
{
  "capture_id": "uuid",
  "target_kind": "uia_element",
  "element_ref": {},
  "region": {"space": "capture_px", "x": 10, "y": 20, "width": 100, "height": 40, "display_id": "display-1", "capture_id": "uuid", "reference_width": 1920, "reference_height": 1080, "transform_version": 7},
  "confidence": 0.94,
  "evidence": ["name_match", "control_type_match"],
  "alternatives": []
}
```

## Annotation

Fields: annotation ID, capture/monitor ID, type, coordinate space, geometry, label, step number, style token, expiry condition, and confidence. Geometry must be transformed locally; the model never chooses WPF DIPs.

## Action proposal

```json
{
  "action_id": "uuid",
  "type": "uia.invoke",
  "target": {},
  "arguments": {},
  "rationale": "User asked to open Export settings",
  "risk_tier": 1,
  "requires_confirmation": false,
  "preconditions": [],
  "postconditions": [],
  "expires_at": "ISO-8601"
}
```

The Windows client's authoritative `LocalPolicyEngine` recalculates risk and confirmation; it does not trust model-supplied values or the orchestrator's advisory classification.

## Action result

Include proposed/executed timestamps, policy decision, confirmation identity/result, target revalidation, execution method, bounded attempts, postcondition evidence, final status, and redacted error.

## Error

```json
{
  "code": "MODEL_NOT_AVAILABLE",
  "component": "model_adapter",
  "retryable": true,
  "user_message": "The selected local model is not installed.",
  "recommended_action": "open_model_settings",
  "diagnostic_id": "opaque"
}
```

Core codes: protocol mismatch, unauthorized client, request too large, capture denied, protected content, audio unavailable, STT failure, model unavailable, timeout, cancelled, target ambiguous, stale target, policy denied, confirmation rejected, postcondition failed, and internal error.

## Configuration

Separate machine/application configuration, per-user preferences, protected secrets, model manifests, policy, and transient session state. Every persisted schema has a version and migration/rollback test.
