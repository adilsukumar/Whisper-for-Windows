import asyncio
import json
import unittest

import httpx

from ollama_client import stream_chat
from protocol import ProtocolError


class BrokenStream(httpx.AsyncByteStream):
    async def __aiter__(self):
        yield b'{"message":{"content":"partial"},"done":false}\n'
        raise httpx.ReadError('private provider detail')


class OllamaClientTests(unittest.IsolatedAsyncioTestCase):
    async def test_memory_budget_and_unload_requested(self):
        def handler(request):
            payload = json.loads(request.content)
            self.assertEqual(payload['keep_alive'], '10m')
            self.assertEqual(payload['options']['num_ctx'], 8192)
            self.assertEqual(payload['options']['num_batch'], 64)
            self.assertEqual(payload['options']['num_predict'], 256)
            return httpx.Response(200, text='{"message":{"content":"ok"},"done":true}\n')
        self.assertEqual(await self.query(httpx.MockTransport(handler)), 'ok')

    def transport(self, events, status=200):
        body = '\n'.join(json.dumps(event) for event in events)
        return httpx.MockTransport(lambda request: httpx.Response(status, text=body))

    async def query(self, transport, on_chunk=None):
        return await stream_chat(
            'http://localhost:11434', 'test-model', [{'role': 'user', 'content': 'hello'}],
            on_chunk=on_chunk, transport=transport,
        )

    async def test_streams_chunks_and_accepts_terminal_content(self):
        chunks = []

        async def collect(chunk):
            chunks.append(chunk)

        result = await self.query(self.transport([
            {'message': {'content': 'Hello'}, 'done': False},
            {'message': {'content': ' world'}, 'done': True},
        ]), collect)
        self.assertEqual(result, 'Hello world')
        self.assertEqual(chunks, ['Hello', ' world'])

    async def test_accepts_empty_terminal_event_after_content(self):
        result = await self.query(self.transport([
            {'message': {'content': 'answer'}, 'done': False}, {'done': True},
        ]))
        self.assertEqual(result, 'answer')

    async def test_oversized_output_is_not_forwarded(self):
        chunks = []
        async def collect(chunk):
            chunks.append(chunk)
        with self.assertRaises(ProtocolError) as caught:
            await self.query(self.transport([{'message': {'content': 'x' * 65537}, 'done': True}]), collect)
        self.assertEqual(caught.exception.code, 'MODEL_RESPONSE_TOO_LARGE')
        self.assertEqual(chunks, [])

    async def test_incomplete_stream_is_not_success(self):
        for events in ([], [{'message': {'content': 'partial'}, 'done': False}]):
            with self.subTest(events=events), self.assertRaises(ProtocolError) as caught:
                await self.query(self.transport(events))
            self.assertEqual(caught.exception.code, 'MODEL_STREAM_INCOMPLETE')

    async def test_provider_error_is_redacted(self):
        with self.assertRaises(ProtocolError) as caught:
            await self.query(self.transport([
                {'message': {'content': 'partial'}, 'done': False},
                {'error': 'secret local path and user text'},
            ]))
        self.assertEqual(caught.exception.code, 'MODEL_INFERENCE_FAILED')
        self.assertNotIn('secret', str(caught.exception))

    async def test_invalid_stream_events(self):
        for event in ([], {'done': 'true'}, {'done': True, 'message': []},
                      {'done': True, 'message': {'content': 42}}):
            with self.subTest(event=event), self.assertRaises(ProtocolError) as caught:
                await self.query(self.transport([event]))
            self.assertEqual(caught.exception.code, 'MODEL_INVALID_RESPONSE')

    async def test_invalid_json(self):
        transport = httpx.MockTransport(lambda request: httpx.Response(200, text='not json\n'))
        with self.assertRaises(ProtocolError) as caught:
            await self.query(transport)
        self.assertEqual(caught.exception.code, 'MODEL_INVALID_RESPONSE')

    async def test_empty_answer_is_not_success(self):
        with self.assertRaises(ProtocolError) as caught:
            await self.query(self.transport([{'done': True, 'message': {'content': ' '}}]))
        self.assertEqual(caught.exception.code, 'MODEL_EMPTY_RESPONSE')

    async def test_http_statuses(self):
        for status, retryable in ((404, False), (500, True)):
            with self.subTest(status=status), self.assertRaises(ProtocolError) as caught:
                await self.query(self.transport([], status))
            self.assertEqual(caught.exception.code, 'MODEL_REQUEST_FAILED')
            self.assertEqual(caught.exception.retryable, retryable)

    async def test_transport_errors(self):
        for error, expected in ((httpx.ConnectError('private'), 'MODEL_SERVICE_UNAVAILABLE'),
                                (httpx.ReadTimeout('private'), 'MODEL_TIMEOUT')):
            def fail(request):
                raise error

            with self.subTest(error=type(error).__name__), self.assertRaises(ProtocolError) as caught:
                await self.query(httpx.MockTransport(fail))
            self.assertEqual(caught.exception.code, expected)
            self.assertNotIn('private', str(caught.exception))

    async def test_connection_lost_after_partial_text(self):
        transport = httpx.MockTransport(lambda request: httpx.Response(200, stream=BrokenStream()))
        with self.assertRaises(ProtocolError) as caught:
            await self.query(transport)
        self.assertEqual(caught.exception.code, 'MODEL_CONNECTION_LOST')

    async def test_cancellation_propagates(self):
        async def cancel(chunk):
            raise asyncio.CancelledError()

        with self.assertRaises(asyncio.CancelledError):
            await self.query(self.transport([{'message': {'content': 'part'}, 'done': False}]), cancel)

    async def test_images_do_not_mutate_conversation(self):
        messages = [{'role': 'user', 'content': 'hello'}]
        images = ['image-data']

        def handler(request):
            payload = json.loads(request.content)
            self.assertEqual(payload['messages'][-1]['images'], images)
            self.assertEqual(payload['options']['num_ctx'], 8192)
            self.assertEqual(payload['options']['num_batch'], 64)
            self.assertEqual(payload['keep_alive'], '10m')
            self.assertEqual(request.extensions['timeout']['read'], 300.0)
            return httpx.Response(200, text='{"message":{"content":"answer"},"done":true}\n')

        await stream_chat('http://localhost:11434/', 'model', messages, images,
                          transport=httpx.MockTransport(handler))
        self.assertNotIn('images', messages[-1])


if __name__ == '__main__':
    unittest.main()
