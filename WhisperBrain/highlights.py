"""Validate untrusted model annotations before emitting typed client events.

This is structural validation, not proof that a model found the correct UI element.
"""
import json

MAX_HIGHLIGHTS = 32
MAX_BLOCK_CHARACTERS = 32_768
MAX_COORDINATE = 32_768
MARKER = "```highlights"


def parse_highlights(text: str) -> list[dict] | None:
    start = text.find(MARKER)
    if start < 0:
        return None
    start += len(MARKER)
    end = text.find("```", start)
    if end < 0 or end - start > MAX_BLOCK_CHARACTERS:
        return None
    try:
        raw = json.loads(text[start:end].strip())
    except (ValueError, RecursionError):
        return None
    if not isinstance(raw, list):
        return None
    valid = []
    for item in raw[:MAX_HIGHLIGHTS]:
        if not isinstance(item, dict) or item.get("type", "box") not in ("box", "pulse"):
            continue
        kind = item.get("type", "box")
        coordinates = {key: item.get(key, 0) for key in ("x", "y", "width", "height")}
        if any(type(value) is not int or not 0 <= value <= MAX_COORDINATE for value in coordinates.values()):
            continue
        if "x" not in item or "y" not in item:
            continue
        if kind == "box" and (coordinates["width"] == 0 or coordinates["height"] == 0):
            continue
        label = item.get("label")
        if label is not None and not isinstance(label, str):
            continue
        valid.append({**coordinates, "type": kind, "label": label[:200] if label else None})
    return valid or None


def remove_highlights_block(text: str) -> str:
    start = text.find(MARKER)
    if start < 0:
        return text
    end = text.find("```", start + len(MARKER))
    # Never expose an unfinished coordinate block as the completed spoken answer.
    return text[:start].rstrip() if end < 0 else (text[:start].rstrip() + "\n" + text[end + 3:].lstrip()).strip()
