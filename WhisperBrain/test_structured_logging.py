import unittest
from unittest.mock import patch

import structured_logging


class StructuredLoggingTests(unittest.TestCase):
    def test_sensitive_fields_are_removed(self):
        with patch.object(structured_logging.logger, "info") as info:
            structured_logging.log_event(
                "request.test",
                request_id="request-1",
                text="private",
                screenshot_base64="secret-image",
                auth_token="secret-token",
            )
        payload = info.call_args.args[0]
        self.assertIn("request-1", payload)
        self.assertNotIn("private", payload)
        self.assertNotIn("secret-image", payload)
        self.assertNotIn("secret-token", payload)


if __name__ == "__main__":
    unittest.main()
