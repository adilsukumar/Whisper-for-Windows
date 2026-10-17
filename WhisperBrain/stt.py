"""Local speech-to-text adapter with lazy faster-whisper loading."""

from __future__ import annotations

import asyncio
import base64
import binascii
from dataclasses import dataclass
import os
from pathlib import Path
import tempfile
from threading import Lock
from typing import Any

from protocol import ProtocolError

MAX_AUDIO_BYTES = 10 * 1024 * 1024


@dataclass(frozen=True, slots=True)
class TranscriptionResult:
    text: str
    language: str | None
    language_probability: float | None


def decode_wav_base64(audio_base64: str) -> bytes:
    """Decode and minimally validate a bounded RIFF/WAVE payload."""
    if not isinstance(audio_base64, str) or not audio_base64.strip():
        raise ProtocolError("INVALID_AUDIO", "The audio recording is empty.")

    try:
        audio = base64.b64decode(audio_base64, validate=True)
    except (binascii.Error, ValueError) as error:
        raise ProtocolError("INVALID_AUDIO", "The audio recording is not valid base64.") from error

    if len(audio) > MAX_AUDIO_BYTES:
        raise ProtocolError(
            "AUDIO_TOO_LARGE",
            f"The audio recording exceeds the {MAX_AUDIO_BYTES // (1024 * 1024)} MB limit.",
        )
    if len(audio) < 12 or audio[:4] != b"RIFF" or audio[8:12] != b"WAVE":
        raise ProtocolError("INVALID_AUDIO", "The audio recording must be a valid WAV file.")

    return audio


class FasterWhisperTranscriber:
    """Replaceable STT adapter; the large runtime loads only on first voice request."""

    def __init__(self) -> None:
        self.model_name = os.environ.get("WHISPER_STT_MODEL", "base")
        self.device = os.environ.get("WHISPER_STT_DEVICE", "cpu")
        self.compute_type = os.environ.get("WHISPER_STT_COMPUTE_TYPE", "int8")
        models_dir = os.environ.get("WHISPER_STT_MODELS_DIR")
        self.models_dir = Path(models_dir) if models_dir else None
        self._model: Any | None = None
        self._model_lock = Lock()

    def _get_model(self) -> Any:
        if self._model is not None:
            return self._model

        with self._model_lock:
            if self._model is not None:
                return self._model
            try:
                from faster_whisper import WhisperModel
            except ImportError as error:
                raise ProtocolError(
                    "STT_NOT_INSTALLED",
                    "Local transcription is not installed. Run: pip install -r WhisperBrain/requirements.stt.txt",
                ) from error

            try:
                model_kwargs: dict[str, Any] = {
                    "device": self.device,
                    "compute_type": self.compute_type,
                }
                if self.models_dir is not None:
                    self.models_dir.mkdir(parents=True, exist_ok=True)
                    model_kwargs["download_root"] = str(self.models_dir)
                self._model = WhisperModel(self.model_name, **model_kwargs)
            except Exception as error:
                raise ProtocolError(
                    "STT_MODEL_UNAVAILABLE",
                    f"Could not load speech model '{self.model_name}': {error}",
                    True,
                ) from error

        return self._model

    def _transcribe_wav(self, audio: bytes) -> TranscriptionResult:
        model = self._get_model()
        temporary_path: str | None = None
        try:
            with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as temporary_file:
                temporary_file.write(audio)
                temporary_path = temporary_file.name

            segments, info = model.transcribe(
                temporary_path,
                beam_size=5,
                vad_filter=True,
                condition_on_previous_text=False,
            )
            text = " ".join(segment.text.strip() for segment in segments if segment.text.strip()).strip()
            if not text:
                raise ProtocolError("NO_SPEECH_DETECTED", "No speech was detected in the recording.")

            return TranscriptionResult(
                text=text,
                language=getattr(info, "language", None),
                language_probability=getattr(info, "language_probability", None),
            )
        finally:
            if temporary_path is not None:
                try:
                    os.unlink(temporary_path)
                except FileNotFoundError:
                    pass

    async def transcribe_base64_wav(self, audio_base64: str) -> TranscriptionResult:
        audio = decode_wav_base64(audio_base64)
        return await asyncio.to_thread(self._transcribe_wav, audio)


transcriber = FasterWhisperTranscriber()
