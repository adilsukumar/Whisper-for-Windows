"""Validated Ollama streaming adapter, independent of server startup/auth state."""

from __future__ import annotations

import json
from collections.abc import Awaitable, Callable

import httpx

from protocol import ProtocolError


async def stream_chat(
    base_url: str,
    model: str,
    messages: list[dict],
    images: list[str] | None = None,
    on_chunk: Callable[[str], Awaitable[None]] | None = None,
    *,
    transport: httpx.AsyncBaseTransport | None = None,
) -> str:
    # Attaching a screenshot must not mutate stored conversation messages.
    outgoing_messages = [dict(message) for message in messages]
    if images and outgoing_messages and outgoing_messages[-1].get('role') == 'user':
        outgoing_messages[-1]['images'] = list(images)

    payload = {
        'model': model,
        'messages': outgoing_messages,
        'stream': True,
        # Keep one small model warm briefly, not indefinitely. Use the same cache
        # size for both paths so switching screen context doesn't reload the runner.
        'keep_alive': '10m',
        'options': {'temperature': 0.7, 'num_predict': 256,
                    'num_ctx': 8192, 'num_batch': 64},
    }
    chunks: list[str] = []
    completed = False
    characters = 0
    # CPU vision preprocessing can exceed two minutes before the first token.
    timeout = httpx.Timeout(300.0 if images else 120.0, connect=10.0)
    try:
        async with httpx.AsyncClient(timeout=timeout, transport=transport) as client:
            async with client.stream('POST', f'{base_url.rstrip("/")}/api/chat', json=payload) as response:
                response.raise_for_status()
                async for line in response.aiter_lines():
                    if not line.strip():
                        continue
                    event = json.loads(line)
                    if not isinstance(event, dict):
                        raise ProtocolError('MODEL_INVALID_RESPONSE', 'Ollama returned an invalid streaming event.', True)
                    if 'error' in event:
                        # Never expose raw provider messages, which may contain local paths/content.
                        raise ProtocolError('MODEL_INFERENCE_FAILED', 'Ollama could not finish this response. Try again.', True)
                    if not isinstance(event.get('done'), bool):
                        raise ProtocolError('MODEL_INVALID_RESPONSE', 'Ollama returned an invalid completion marker.', True)
                    message = event.get('message', {})
                    if not isinstance(message, dict) or not isinstance(message.get('content', ''), str):
                        raise ProtocolError('MODEL_INVALID_RESPONSE', 'Ollama returned invalid response content.', True)
                    chunk = message.get('content', '')
                    if chunk:
                        characters += len(chunk)
                        if characters > 65536:
                            raise ProtocolError('MODEL_RESPONSE_TOO_LARGE', 'The model response exceeded the output limit. Ask a shorter question.', True)
                        chunks.append(chunk)
                        if on_chunk is not None:
                            await on_chunk(chunk)
                    if event['done']:
                        completed = True
                        break
    except httpx.TimeoutException as exc:
        raise ProtocolError('MODEL_TIMEOUT', 'The model request timed out. It may still be loading.', True) from exc
    except httpx.ConnectError as exc:
        raise ProtocolError('MODEL_SERVICE_UNAVAILABLE', 'Cannot connect to Ollama. Start Ollama and try again.', True) from exc
    except httpx.HTTPStatusError as exc:
        status = exc.response.status_code
        message = 'The configured model is unavailable.' if status == 404 else 'Ollama rejected the request.'
        raise ProtocolError('MODEL_REQUEST_FAILED', message, status >= 500) from exc
    except httpx.RequestError as exc:
        raise ProtocolError('MODEL_CONNECTION_LOST', 'The connection to Ollama was interrupted. Try again.', True) from exc
    except json.JSONDecodeError as exc:
        raise ProtocolError('MODEL_INVALID_RESPONSE', 'Ollama returned invalid streaming data.', True) from exc

    if not completed:
        raise ProtocolError('MODEL_STREAM_INCOMPLETE', 'The model response ended before completion. Try again.', True)
    answer = ''.join(chunks)
    if not answer.strip():
        raise ProtocolError('MODEL_EMPTY_RESPONSE', 'The model returned no answer. Try again.', True)
    return answer
