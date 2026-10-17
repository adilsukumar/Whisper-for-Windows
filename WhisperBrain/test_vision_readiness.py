import json
import unittest
import httpx
from vision_readiness import check_vision, red_test_image


class VisionReadinessTests(unittest.IsolatedAsyncioTestCase):
    async def check(self, tags, show=None, answer=None, inference=True):
        self.paths = []
        def handler(request):
            self.paths.append(request.url.path)
            if request.url.path == '/api/tags': data = tags
            elif request.url.path == '/api/show': data = show
            else:
                payload = json.loads(request.content)
                self.assertEqual(payload['messages'][0]['images'], [red_test_image()])
                self.assertFalse(payload['stream'])
                self.assertEqual(payload['keep_alive'], 0)
                self.assertEqual(payload['options']['num_ctx'], 2048)
                self.assertEqual(payload['options']['num_batch'], 64)
                data = answer
            return httpx.Response(200, json=data)
        return await check_vision('llama3.2-vision', inference=inference, transport=httpx.MockTransport(handler))

    async def test_missing_model_never_calls_inference(self):
        result = await self.check({'models': [{'name': 'llama3.1:latest'}]})
        self.assertFalse(result['installed']); self.assertFalse(result['ready'])
        self.assertEqual(self.paths, ['/api/tags'])

    async def test_text_model_is_not_vision_ready(self):
        result = await self.check({'models': [{'name': 'llama3.2-vision:latest'}]}, {'capabilities': ['completion']})
        self.assertFalse(result['vision_capable']); self.assertFalse(result['ready'])
        self.assertNotIn('/api/chat', self.paths)

    async def test_installed_does_not_claim_inference_verified(self):
        result = await self.check({'models': [{'name': 'llama3.2-vision:latest'}]}, {'capabilities': ['vision']}, inference=False)
        self.assertTrue(result['installed']); self.assertFalse(result['ready'])
        self.assertEqual(result['inference'], 'not_run')

    async def test_actual_synthetic_image_pass(self):
        result = await self.check({'models': [{'name': 'llama3.2-vision:latest'}]}, {'capabilities': ['vision']},
                                  {'done': True, 'message': {'content': 'red'}})
        self.assertTrue(result['ready'])

    async def test_wrong_or_incomplete_answer_is_not_ready(self):
        for done, content in [(False, 'red'), (True, 'blue'), (True, ''), (True, None), (True, 'not red')]:
            result = await self.check({'models': [{'name': 'llama3.2-vision:latest'}]}, {'capabilities': ['vision']},
                                      {'done': done, 'message': {'content': content}})
            self.assertFalse(result['ready'])

    async def test_inventory_is_bounded(self):
        def handler(request): return httpx.Response(200, content=b'x' * (1024 * 1024 + 1))
        with self.assertRaises(ValueError):
            await check_vision(transport=httpx.MockTransport(handler))

    async def test_nonlocal_ollama_url_rejected(self):
        for url in ('https://example.com:11434', 'http://example.com:11434',
                    'http://127.0.0.1:11434/extra', 'http://user:pass@127.0.0.1:11434'):
            with self.subTest(url=url), self.assertRaises(ValueError):
                await check_vision(ollama_url=url)
