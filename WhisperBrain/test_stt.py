import base64
import unittest

from protocol import ProtocolError
from stt import MAX_AUDIO_BYTES, decode_wav_base64


class SttValidationTests(unittest.TestCase):
    def test_rejects_invalid_base64(self):
        with self.assertRaises(ProtocolError) as caught:
            decode_wav_base64("not base64!")
        self.assertEqual(caught.exception.code, "INVALID_AUDIO")

    def test_rejects_non_wav_audio(self):
        payload = base64.b64encode(b"this is not wav audio").decode("ascii")
        with self.assertRaises(ProtocolError) as caught:
            decode_wav_base64(payload)
        self.assertEqual(caught.exception.code, "INVALID_AUDIO")

    def test_rejects_audio_above_limit(self):
        oversized_wav = b"RIFF" + b"\0" * 4 + b"WAVE" + b"\0" * MAX_AUDIO_BYTES
        payload = base64.b64encode(oversized_wav).decode("ascii")
        with self.assertRaises(ProtocolError) as caught:
            decode_wav_base64(payload)
        self.assertEqual(caught.exception.code, "AUDIO_TOO_LARGE")

    def test_accepts_wav_container_header(self):
        wav = b"RIFF" + b"\0" * 4 + b"WAVE" + b"fmt "
        self.assertEqual(decode_wav_base64(base64.b64encode(wav).decode("ascii")), wav)


if __name__ == "__main__":
    unittest.main()
