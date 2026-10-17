import json
import unittest
from highlights import parse_highlights, remove_highlights_block


class HighlightTests(unittest.TestCase):
    def block(self, value):
        return "Answer\n```highlights\n" + json.dumps(value) + "\n```"

    def test_valid_box_and_pulse(self):
        result = parse_highlights(self.block([{"x": 10, "y": 20, "width": 30, "height": 40}, {"x": 1, "y": 2, "type": "pulse"}]))
        self.assertEqual(result[0]["type"], "box")
        self.assertEqual(result[1]["width"], 0)

    def test_wrong_roots_and_invalid_json(self):
        for value in ({"x": 1}, "secret", None, 123):
            self.assertIsNone(parse_highlights(self.block(value)))
        self.assertIsNone(parse_highlights("```highlights\n[invalid]\n```"))

    def test_wrong_types_and_coordinates(self):
        base = {"x": 1, "y": 2, "width": 3, "height": 4, "type": "box"}
        for key, value in (("x", True), ("x", 1.5), ("x", -1), ("y", 2**40), ("width", 0), ("height", -2), ("type", "command"), ("label", {"secret": 1})):
            self.assertIsNone(parse_highlights(self.block([{**base, key: value}])))

    def test_missing_coordinates(self):
        self.assertIsNone(parse_highlights(self.block([{"type": "pulse"}])))

    def test_sanitizes_extra_fields_and_label(self):
        result = parse_highlights(self.block([{"x": 1, "y": 2, "type": "pulse", "label": "a" * 250, "action": "execute"}]))
        self.assertEqual(len(result[0]["label"]), 200)
        self.assertNotIn("action", result[0])

    def test_count_and_block_limits(self):
        self.assertEqual(len(parse_highlights(self.block([{"x": 1, "y": 2, "type": "pulse"}] * 40))), 32)
        self.assertIsNone(parse_highlights("```highlights\n" + " " * 32769 + "```"))

    def test_complete_and_incomplete_blocks_removed(self):
        self.assertEqual(remove_highlights_block(self.block([])), "Answer")
        self.assertEqual(remove_highlights_block("Answer\n```highlights\n["), "Answer")
        self.assertEqual(remove_highlights_block("plain answer"), "plain answer")

    def test_bad_items_do_not_discard_valid_items(self):
        result = parse_highlights(self.block([False, "bad", {"x": 1, "y": 2, "type": "pulse"}]))
        self.assertEqual(len(result), 1)
