"""Versioned wire protocol helpers for the Whisper local orchestrator."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
import hmac
import re
from typing import Any

PROTOCOL_VERSION = "1.0"
SUPPORTED_MAJOR = 1

REQUEST_START = "request.start"
REQUEST_CANCEL = "request.cancel"
TRANSCRIPT_FINAL = "transcript.final"
RESPONSE_STARTED = "response.started"
RESPONSE_DELTA = "response.delta"
RESPONSE_COMPLETED = "response.completed"
REQUEST_CANCELLED = "request.cancelled"
ERROR = "error"

ALLOWED_CLIENT_EVENTS = {REQUEST_START, REQUEST_CANCEL}
MAX_MEDIA_CHARACTERS = 16 * 1024 * 1024
MAX_MESSAGE_BYTES = MAX_MEDIA_CHARACTERS + 512 * 1024  # Headroom for JSON-escaped Unicode text.


@dataclass(slots=True)
class ProtocolError(Exception):
    code: str
    message: str
    retryable: bool = False

    def __str__(self) -> str:
        return self.message


def utc_timestamp() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def parse_client_envelope(raw: Any, expected_auth_token: str | None = None) -> dict[str, Any]:
    if not isinstance(raw, dict):
        raise ProtocolError("INVALID_REQUEST", "Request must be a JSON object.")

    version = raw.get("protocol_version", "")
    if not isinstance(version, str) or not re.fullmatch(r"\d{1,3}\.\d{1,3}", version):
        raise ProtocolError("PROTOCOL_MISMATCH", "A valid protocol_version is required.")
    try:
        major = int(version.split(".", 1)[0])
    except (ValueError, IndexError):
        raise ProtocolError("PROTOCOL_MISMATCH", "A valid protocol_version is required.")

    if major != SUPPORTED_MAJOR:
        raise ProtocolError(
            "PROTOCOL_MISMATCH",
            "The client protocol is not compatible with this server.",
        )

    if expected_auth_token is not None:
        supplied_token = raw.get("auth_token")
        if not isinstance(supplied_token, str) or len(supplied_token) > 256 or not supplied_token.isascii() or not hmac.compare_digest(
            supplied_token.encode("utf-8"),
            expected_auth_token.encode("utf-8"),
        ):
            raise ProtocolError("UNAUTHORIZED_CLIENT", "Local client authentication failed.")

    event_type = raw.get("event_type")
    account_token = raw.get('account_token')
    if account_token is not None and (not isinstance(account_token, str) or not re.fullmatch(r'[A-Za-z0-9_-]{43}', account_token)):
        raise ProtocolError('INVALID_REQUEST', 'Account session token is invalid.')
    if not isinstance(event_type, str) or event_type not in ALLOWED_CLIENT_EVENTS:
        raise ProtocolError("INVALID_EVENT", "Unsupported client event.")

    for field in ("session_id", "request_id"):
        value = raw.get(field)
        if not isinstance(value, str) or not value.strip() or len(value) > 128:
            raise ProtocolError("INVALID_REQUEST", f"{field} must be a non-empty string.")

    for field, limit in (("text", 32768), ("active_window", 512), ("screen_text", 3000), ("guide_goal", 1000)):
        value = raw.get(field)
        if value is not None and (not isinstance(value, str) or len(value) > limit):
            raise ProtocolError("INVALID_REQUEST", f"{field} has an invalid type or length.")
    media_size = 0
    for field, limit in (("audio_base64", 14 * 1024 * 1024), ("screenshot_base64", 8 * 1024 * 1024)):
        value = raw.get(field)
        if value is None:
            continue
        if not isinstance(value, str) or len(value) > limit:
            raise ProtocolError("INVALID_REQUEST", f"{field} has an invalid type or length.")
        media_size += len(value)
        if media_size > MAX_MEDIA_CHARACTERS:
            raise ProtocolError("REQUEST_TOO_LARGE", "Combined audio and screen data exceeds the request limit.")
        if value and (len(value) % 4 or not re.fullmatch(r"[A-Za-z0-9+/]*={0,2}", value)):
            raise ProtocolError("INVALID_REQUEST", f"{field} must contain base64 data.")
    capture = raw.get("capture")
    guide = raw.get('guide', False)
    clicked = raw.get('guide_clicked')
    if clicked is not None and (not isinstance(clicked, list) or len(clicked) > 24
        or any(not isinstance(label, str) or not 1 <= len(label) <= 200 for label in clicked)):
        raise ProtocolError('INVALID_REQUEST', 'Guide click history is invalid.')
    failed = raw.get('guide_failed')
    if failed is not None and (not isinstance(failed, list) or len(failed) > 24
        or any(not isinstance(label, str) or not 1 <= len(label) <= 200 for label in failed)):
        raise ProtocolError('INVALID_REQUEST', 'Guide failed-step history is invalid.')
    if type(guide) is not bool:
        raise ProtocolError('INVALID_REQUEST', 'Guide mode must be a boolean.')
    targets = raw.get('screen_targets')
    if targets is not None:
        if not isinstance(targets, list) or len(targets) > 24 or not raw.get('screenshot_base64'):
            raise ProtocolError('INVALID_REQUEST', 'Screen controls require a consented screenshot.')
        ids = set()
        validated = []
        for target in targets:
            if (not isinstance(target, dict) or not isinstance(target.get('id'), str)
                or not re.fullmatch(r't\d{1,2}', target['id']) or target['id'] in ids
                or not isinstance(target.get('label'), str) or not 1 <= len(target['label']) <= 200):
                raise ProtocolError('INVALID_REQUEST', 'Screen control identity is invalid.')
            ids.add(target['id'])
            state = target.get('state')
            if state not in (None, 'on', 'off', 'indeterminate'):
                raise ProtocolError('INVALID_REQUEST', 'Screen control state is invalid.')
            item = {'id': target['id'], 'label': target['label']}
            if state is not None: item['state'] = state
            validated.append(item)
        raw = {**raw, 'screen_targets': validated}
    if guide and not raw.get('screenshot_base64'):
        raise ProtocolError('INVALID_REQUEST', 'Guide mode requires screen consent.')
    if capture is not None:
        if not raw.get("screenshot_base64") or not isinstance(capture, dict):
            raise ProtocolError("INVALID_CAPTURE", "Capture metadata requires a screenshot.")
        for field in ("capture_id", "monitor_id"):
            value = capture.get(field)
            if not isinstance(value, str) or not value or len(value) > 128 or not value.isascii():
                raise ProtocolError("INVALID_CAPTURE", "Capture identity is invalid.")
        for field in ("origin_x", "origin_y", "width", "height"):
            value = capture.get(field)
            lower = -1_000_000 if field.startswith("origin") else 1
            upper = 1_000_000 if field.startswith("origin") else 32768
            if type(value) is not int or not lower <= value <= upper:
                raise ProtocolError("INVALID_CAPTURE", "Capture bounds are invalid.")
        if capture.get("coordinate_space") != "capture_pixels" or capture["width"] * capture["height"] > 40_000_000:
            raise ProtocolError("INVALID_CAPTURE", "Unsupported capture coordinate space or size.")
        raw = {**raw, "capture": {key: capture[key] for key in
            ("capture_id", "monitor_id", "origin_x", "origin_y", "width", "height", "coordinate_space")}}
    return raw


def make_event(
    event_type: str,
    session_id: str,
    request_id: str,
    sequence: int,
    **payload: Any,
) -> dict[str, Any]:
    return {
        "protocol_version": PROTOCOL_VERSION,
        "event_type": event_type,
        "session_id": session_id,
        "request_id": request_id,
        "sequence": sequence,
        "timestamp": utc_timestamp(),
        **payload,
    }


def make_error_event(
    session_id: str,
    request_id: str,
    sequence: int,
    error: ProtocolError,
) -> dict[str, Any]:
    return make_event(
        ERROR,
        session_id,
        request_id,
        sequence,
        error={
            "code": error.code,
            "message": error.message,
            "retryable": error.retryable,
        },
    )
