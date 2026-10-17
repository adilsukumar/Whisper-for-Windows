"""Exercise real main() startup without a live model, network listener, or permanent token."""
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


class ServerStartupTests(unittest.TestCase):
    def test_main_reaches_listener_after_banner(self):
        code = '''
import asyncio
import server
assert server.OLLAMA_BASE_URL == "http://127.0.0.1:11435"
active = 0
peak = 0
async def fake_chat(*args, **kwargs):
    global active, peak
    active += 1
    peak = max(peak, active)
    try:
        await asyncio.sleep(0.01)
        return "ok"
    finally:
        active -= 1
async def check_serialization():
    server.stream_chat = fake_chat
    answers = await asyncio.gather(server.query_ollama_chat("text", []),
                                   server.query_ollama_chat("vision", []))
    assert answers == ["ok", "ok"] and peak == 1
asyncio.run(check_serialization())
class ListenerReached(Exception): pass
async def healthy(): return True
class FakeServer:
    async def __aenter__(self): raise ListenerReached()
    async def __aexit__(self, *args): return False
server.check_ollama_health = healthy
server.websockets.serve = lambda *args, **kwargs: FakeServer()
try:
    asyncio.run(server.main())
except ListenerReached:
    print("STARTUP_REACHED_LISTENER")
'''
        with tempfile.TemporaryDirectory(prefix="whisper-startup-test-") as directory:
            environment = dict(os.environ, PYTHONUTF8="0", PYTHONIOENCODING="cp1252",
                WHISPER_AUTH_TOKEN_FILE=str(Path(directory) / "test.token"), WHISPER_PORT="18765")
            environment.pop("OLLAMA_URL", None)
            result = subprocess.run([sys.executable, "-c", code], cwd=Path(__file__).parent,
                env=environment, capture_output=True, encoding="utf-8", timeout=20)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn("STARTUP_REACHED_LISTENER", result.stdout)
            self.assertIn("18765", result.stdout)
