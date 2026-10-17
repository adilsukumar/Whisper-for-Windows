"""Content-free structured logging for the local orchestrator."""

from __future__ import annotations

import json
import logging
from datetime import datetime, timezone
from typing import Any

logger = logging.getLogger("whisper.orchestrator")
if not logger.handlers:
    handler = logging.StreamHandler()
    handler.setFormatter(logging.Formatter("%(message)s"))
    logger.addHandler(handler)
logger.setLevel(logging.INFO)
logger.propagate = False

_FORBIDDEN_KEYS = {
    "text",
    "transcript",
    "audio",
    "audio_base64",
    "screenshot",
    "screenshot_base64",
    "auth_token",
    "window_title",
    "file_path",
}


def log_event(event: str, **fields: Any) -> None:
    safe_fields = {
        key: value
        for key, value in fields.items()
        if key.lower() not in _FORBIDDEN_KEYS
    }
    record = {
        "timestamp": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "event": event,
        **safe_fields,
    }
    logger.info(json.dumps(record, separators=(",", ":"), default=str))
