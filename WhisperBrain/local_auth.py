"""Per-install local authentication token management.

This protects the prototype loopback endpoint from accidental/untrusted local
clients. Production hardening still requires explicit Windows ACL tests and a
protected secret-store decision.
"""

from __future__ import annotations

import os
import secrets
from pathlib import Path


def default_token_path() -> Path:
    configured = os.environ.get("WHISPER_AUTH_TOKEN_FILE")
    if configured:
        return Path(configured).expanduser()

    local_app_data = os.environ.get("LOCALAPPDATA")
    base = Path(local_app_data) if local_app_data else Path.home() / ".whisper"
    return base / "Whisper" / "orchestrator.token"


def load_or_create_token(path: Path | None = None) -> str:
    token_path = path or default_token_path()
    token_path.parent.mkdir(parents=True, exist_ok=True)

    if token_path.exists():
        token = token_path.read_text(encoding="utf-8").strip()
        if len(token) < 32:
            raise RuntimeError(f"Authentication token at {token_path} is invalid.")
        return token

    token = secrets.token_urlsafe(48)
    temporary = token_path.with_suffix(".tmp")
    temporary.write_text(token, encoding="utf-8")
    try:
        os.chmod(temporary, 0o600)
    except OSError:
        pass
    temporary.replace(token_path)
    return token
