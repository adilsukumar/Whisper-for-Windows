import unittest

from protocol import (
    PROTOCOL_VERSION,
    REQUEST_CANCEL,
    REQUEST_START,
    ProtocolError,
    make_error_event,
    parse_client_envelope,
)


class ProtocolTests(unittest.TestCase):
    def valid_request(self, event_type: str = REQUEST_START):
        return {
            "protocol_version": PROTOCOL_VERSION,
            "event_type": event_type,
            "session_id": "session-1",
            "request_id": "request-1",
        }

    def test_accepts_start_and_cancel(self):
        self.assertEqual(parse_client_envelope(self.valid_request())["event_type"], REQUEST_START)
        self.assertEqual(
            parse_client_envelope(self.valid_request(REQUEST_CANCEL))["event_type"],
            REQUEST_CANCEL,
        )

    def test_rejects_incompatible_version(self):
        request = self.valid_request()
        request["protocol_version"] = "2.0"
        with self.assertRaises(ProtocolError) as caught:
            parse_client_envelope(request)
        self.assertEqual(caught.exception.code, "PROTOCOL_MISMATCH")

    def test_rejects_missing_identity(self):
        request = self.valid_request()
        request["request_id"] = ""
        with self.assertRaises(ProtocolError) as caught:
            parse_client_envelope(request)
        self.assertEqual(caught.exception.code, "INVALID_REQUEST")

    def test_requires_matching_local_auth_token(self):
        request = self.valid_request()
        request["auth_token"] = "correct-token-value-that-is-long-enough"
        self.assertEqual(
            parse_client_envelope(request, "correct-token-value-that-is-long-enough")["request_id"],
            "request-1",
        )
        request["auth_token"] = "wrong-token"
        with self.assertRaises(ProtocolError) as caught:
            parse_client_envelope(request, "correct-token-value-that-is-long-enough")
        self.assertEqual(caught.exception.code, "UNAUTHORIZED_CLIENT")

    def test_error_event_is_structured(self):
        event = make_error_event(
            "session-1",
            "request-1",
            3,
            ProtocolError("MODEL_NOT_AVAILABLE", "Model is unavailable.", True),
        )
        self.assertEqual(event["error"]["code"], "MODEL_NOT_AVAILABLE")
        self.assertTrue(event["error"]["retryable"])


    def test_unhashable_event_is_structured_failure(self):
        for event in ([], {}, 1, None):
            with self.assertRaises(ProtocolError):
                parse_client_envelope({**self.valid_request(), "event_type": event})

    def test_unicode_auth_fails_without_type_error(self):
        for token in ("🔑", "\ud800", "x" * 257):
            with self.assertRaises(ProtocolError) as caught:
                parse_client_envelope({**self.valid_request(), "auth_token": token}, "expected-token")
            self.assertEqual(caught.exception.code, "UNAUTHORIZED_CLIENT")

    def test_payload_types_and_lengths(self):
        for field, value in (("text", []), ("text", "x" * 32769), ("active_window", {}), ("active_window", "x" * 513), ("audio_base64", True), ("screenshot_base64", "bad!")):
            with self.assertRaises(ProtocolError):
                parse_client_envelope({**self.valid_request(), field: value})

    def test_valid_media_and_combined_bound(self):
        request = {**self.valid_request(), "audio_base64": "AAAA", "screenshot_base64": "AAAA"}
        self.assertEqual(parse_client_envelope(request), request)
        with self.assertRaises(ProtocolError) as caught:
            parse_client_envelope({**request, "audio_base64": "A" * (14 * 1024 * 1024), "screenshot_base64": "A" * (3 * 1024 * 1024)})
        self.assertEqual(caught.exception.code, "REQUEST_TOO_LARGE")

    def test_bad_version_is_not_coerced_or_echoed(self):
        for version in (1, {}, None, "1", "2.0"):
            with self.assertRaises(ProtocolError):
                parse_client_envelope({**self.valid_request(), "protocol_version": version})

    def capture_request(self):
        return {**self.valid_request(), 'screenshot_base64': 'AAAA', 'capture': {
            'capture_id': 'capture-1', 'monitor_id': '\\\\.\\DISPLAY2',
            'origin_x': -1920, 'origin_y': -120, 'width': 1920, 'height': 1080,
            'coordinate_space': 'capture_pixels'}}

    def test_capture_negative_monitor_origin_and_normalization(self):
        request = self.capture_request()
        request['capture']['extra'] = 'not forwarded'
        parsed = parse_client_envelope(request)
        self.assertEqual(parsed['capture']['origin_x'], -1920)
        self.assertNotIn('extra', parsed['capture'])

    def test_capture_invalid_bounds_and_identity(self):
        for field, value in [('width', True), ('height', 0), ('origin_x', -1000001),
                             ('monitor_id', ''), ('capture_id', '界'), ('coordinate_space', 'desktop_dips')]:
            request = self.capture_request(); request['capture'][field] = value
            with self.subTest(field=field), self.assertRaises(ProtocolError):
                parse_client_envelope(request)

    def test_capture_requires_screenshot_and_bounded_area(self):
        request = self.capture_request(); request.pop('screenshot_base64')
        with self.assertRaises(ProtocolError): parse_client_envelope(request)
        request = self.capture_request(); request['capture'].update(width=32768, height=32768)
        with self.assertRaises(ProtocolError): parse_client_envelope(request)

if __name__ == "__main__":
    unittest.main()
