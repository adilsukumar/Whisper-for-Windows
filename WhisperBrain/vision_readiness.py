"""Installation and opt-in synthetic-image smoke test; never captures the desktop."""
import argparse
import asyncio
import base64
import json
import struct
import zlib
from urllib.parse import urlsplit

import httpx


def red_test_image():
    """Small RGB PNG made in memory. No photos or screenshots are read."""
    def chunk(kind, data):
        return struct.pack('!I', len(data)) + kind + data + struct.pack('!I', zlib.crc32(kind + data) & 0xffffffff)
    pixels = (b'\0' + b'\xff\0\0' * 64) * 64
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('!2I5B', 64, 64, 8, 2, 0, 0, 0))
    return base64.b64encode(png + chunk(b'IDAT', zlib.compress(pixels)) + chunk(b'IEND', b'')).decode()


async def bounded_json(client, method, path, payload=None):
    async with client.stream(method, path, json=payload) as response:
        response.raise_for_status()
        data = bytearray()
        async for block in response.aiter_bytes():
            data.extend(block)
            if len(data) > 1024 * 1024:
                raise ValueError('Response exceeded the readiness-check limit.')
        result = json.loads(data)
        if not isinstance(result, dict):
            raise ValueError('Expected an object response.')
        return result


async def check_vision(model='qwen3-vl:2b-instruct-q4_K_M', inference=False, *, transport=None,
                       ollama_url='http://127.0.0.1:11435'):
    target = urlsplit(ollama_url)
    if (target.scheme != 'http' or target.hostname not in ('127.0.0.1', 'localhost', '::1')
            or not target.port or target.path not in ('', '/') or target.query or target.fragment
            or target.username or target.password):
        raise ValueError('Vision readiness only connects to a local Ollama HTTP port.')
    canonical = model if ':' in model.rsplit('/', 1)[-1] else model + ':latest'
    async with httpx.AsyncClient(base_url=ollama_url, follow_redirects=False,
                                timeout=httpx.Timeout(300 if inference else 10, connect=5),
                                transport=transport) as client:
        tags = await bounded_json(client, 'GET', '/api/tags')
        models = tags.get('models')
        if not isinstance(models, list):
            raise ValueError('Invalid model inventory.')
        if not any(isinstance(item, dict) and item.get('name') == canonical for item in models):
            return {'model': canonical, 'installed': False, 'vision_capable': False,
                    'inference': 'not_run', 'ready': False, 'reason': 'Download is not verified installed.'}
        shown = await bounded_json(client, 'POST', '/api/show', {'model': canonical})
        capabilities = shown.get('capabilities', [])
        capable = isinstance(capabilities, list) and 'vision' in capabilities
        result = {'model': canonical, 'installed': True, 'vision_capable': capable,
                  'inference': 'not_run', 'ready': False}
        if not capable:
            result['reason'] = 'Installed model does not advertise image input support.'
        elif inference:
            answer = await bounded_json(client, 'POST', '/api/chat', {
                'model': canonical, 'stream': False, 'keep_alive': 0,
                'options': {'temperature': 0, 'num_predict': 32, 'num_ctx': 2048, 'num_batch': 64},
                'messages': [{'role': 'user', 'content': 'What is the dominant color of this image? Answer with one color word.',
                              'images': [red_test_image()]}]})
            message = answer.get('message')
            text = message.get('content', '') if isinstance(message, dict) else ''
            # A successful HTTP response alone does not prove useful vision inference.
            passed = answer.get('done') is True and isinstance(text, str) and text.strip().strip('.!\"').lower() == 'red'
            result.update(inference='passed' if passed else 'failed', ready=passed)
            if not passed:
                result['reason'] = 'The synthetic red-image test did not return the expected completed answer.'
        else:
            result['reason'] = 'Installed; run --inference to verify an actual image response.'
        return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', default='qwen3-vl:2b-instruct-q4_K_M')
    parser.add_argument('--ollama-url', default='http://127.0.0.1:11435',
                        help='Local Ollama URL; for the D:-backed sidecar use http://127.0.0.1:11435.')
    parser.add_argument('--inference', action='store_true', help='Load the installed model and test one synthetic image.')
    args = parser.parse_args()
    try:
        result = asyncio.run(check_vision(args.model, args.inference, ollama_url=args.ollama_url))
    except (httpx.HTTPError, ValueError, TypeError):
        result = {'ready': False, 'reason': 'Local Ollama check failed or timed out. No model/cache files were changed.'}
    print(json.dumps(result, indent=2))
    return 0 if result.get('ready') else 2


if __name__ == '__main__':
    raise SystemExit(main())
