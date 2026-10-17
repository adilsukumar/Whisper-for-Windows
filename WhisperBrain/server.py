"""
Whisper Local Orchestrator — PROTOTYPE
======================================
Runs model inference through a user-controlled Ollama instance by default.

Ollama is MIT licensed, but every model and redistributed artifact has its own
license and notice requirements. Review and record the exact artifact license
before distributing or monetizing a build.

Architecture:
  1. Ollama runs locally or on your cloud GPU server, serving the vision model
  2. This server connects to the dedicated local Ollama API (http://127.0.0.1:11435)
  3. The C# client connects to this server via WebSocket
  4. In this prototype configuration, requests stay on the local machine

To run:
  1. Install Ollama: https://ollama.ai/download
  2. Pull a vision model: ollama pull llava:13b  (or qwen2-vl, llama3.2-vision)
  3. Start this server: python server.py
"""

import asyncio
import json
import os
import sys
import time
from datetime import datetime
import websockets
import httpx

from app_profiles import get_app_profile
from highlights import parse_highlights, remove_highlights_block
from local_auth import default_token_path, load_or_create_token
from ollama_client import stream_chat
from screen_context import use_screen_text, screen_messages
from guidance import guide_messages, parse_step, direct_step, eligible_targets, stalled_step, is_excel, unsupported_excel_step, requested as guidance_requested
from entitlement_gate import EntitlementGate
from structured_logging import log_event
from protocol import (
    ERROR,
    MAX_MESSAGE_BYTES,
    REQUEST_CANCEL,
    REQUEST_CANCELLED,
    REQUEST_START,
    TRANSCRIPT_FINAL,
    RESPONSE_COMPLETED,
    RESPONSE_DELTA,
    RESPONSE_STARTED,
    ProtocolError,
    make_error_event,
    make_event,
    parse_client_envelope,
)
from stt import transcriber

# ======================== CONFIG ========================

OLLAMA_BASE_URL = os.environ.get('OLLAMA_URL', 'http://127.0.0.1:11435')
VISION_MODEL = os.environ.get('WHISPER_VISION_MODEL', 'qwen3-vl:2b-instruct-q4_K_M')
TEXT_MODEL = os.environ.get('WHISPER_TEXT_MODEL', 'qwen3-vl:2b-instruct-q4_K_M')
ACCOUNT_GATE = EntitlementGate(os.environ.get('WHISPER_ACCOUNTS_ORIGIN'),
    os.environ.get('WHISPER_REQUIRE_ACCOUNT') == '1')
WEBSOCKET_PORT = int(os.environ.get('WHISPER_PORT', '8765'))
MAX_CONVERSATION_HISTORY = 10
MAX_CONVERSATIONS = 100
INFERENCE_LOCK = asyncio.Lock()
AUTH_TOKEN = load_or_create_token()

# ======================== MEMORY ========================

conversations: dict[str, list] = {}
conversation_last_used: dict[str, float] = {}


def get_conversation(conv_id: str) -> list:
    conversation_last_used[conv_id] = time.monotonic()
    if conv_id not in conversations:
        if len(conversations) >= MAX_CONVERSATIONS:
            oldest = min(conversation_last_used, key=conversation_last_used.get)
            conversations.pop(oldest, None)
            conversation_last_used.pop(oldest, None)
        conversations[conv_id] = []
    return conversations[conv_id]


def add_to_conversation(conv_id: str, role: str, content: str):
    history = get_conversation(conv_id)
    history.append({'role': role, 'content': content})
    if len(history) > MAX_CONVERSATION_HISTORY * 2:
        conversations[conv_id] = history[-MAX_CONVERSATION_HISTORY * 2:]


# ======================== OLLAMA CLIENT ========================

COORDINATE_INSTRUCTION = """
IMPORTANT: If the user asks about a specific UI element, button, menu item, or area on the screen,
you MUST include a JSON block at the END of your response in this exact format:

```highlights
[{"x": 100, "y": 200, "width": 80, "height": 30, "label": "This button", "type": "box"}]
```

Coordinates are in screen pixels from the top-left corner of the screenshot.
Use "type": "box" for rectangular highlights, "pulse" for pulsing dot indicators.
Only include this block when pointing at specific screen elements. For general Q&A, omit it.
"""


async def query_ollama_chat(
    model: str,
    messages: list[dict],
    images: list[str] | None = None,
    on_chunk=None,
) -> str:
    """
    Chat-style query to Ollama with conversation history support.
    """
    # Serialize this brain's requests so text and vision do not load together.
    # Other Ollama clients remain outside this process's control.
    async with INFERENCE_LOCK:
        return await stream_chat(OLLAMA_BASE_URL, model, messages, images, on_chunk)


# ======================== PROCESSING ========================

async def process_vision_query(
    text: str,
    screenshot_b64: str,
    active_window: str,
    conv_id: str,
    on_chunk=None,
) -> dict:
    """Process a query that includes a screenshot using the local vision model."""

    app_system_prompt = get_app_profile(active_window)
    system_prompt = f"""{app_system_prompt}

The user is currently in: {active_window}
Current time: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}

{COORDINATE_INSTRUCTION}

Start with a short direct answer, usually 1–3 sentences. Expand only when asked.
The image is a reduced-resolution screen preview: do not invent unreadable small text.
If they ask how to do something, give step-by-step instructions and point to the exact UI elements."""

    # Build messages with conversation history
    history = get_conversation(conv_id)

    messages = [{'role': 'system', 'content': system_prompt}]
    for msg in history[-6:]:
        messages.append(msg)

    user_text = text if text else 'What am I looking at? Explain the screen.'
    messages.append({'role': 'user', 'content': user_text})

    start_time = time.time()
    response_text = await query_ollama_chat(
        VISION_MODEL,
        messages,
        images=[screenshot_b64],
        on_chunk=on_chunk,
    )
    elapsed = time.time() - start_time
    print(f"  Vision query took {elapsed:.1f}s")

    # Save to conversation memory
    add_to_conversation(conv_id, 'user', user_text)
    add_to_conversation(conv_id, 'assistant', response_text)

    # Parse highlights from response
    highlights = parse_highlights(response_text)
    clean_text = remove_highlights_block(response_text)

    return {
        'text': clean_text,
        'highlights': highlights,
        'done': True,
        'is_streaming': False
    }


async def process_text_query(text: str, active_window: str, conv_id: str, on_chunk=None) -> dict:
    """Process a text-only query (no screenshot) using the text model."""

    app_system_prompt = get_app_profile(active_window)
    system_prompt = f"""{app_system_prompt}

The user is currently in: {active_window}
Current time: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}

You are Whisper, an incredibly intelligent AI assistant. Answer any question the user asks -
whether it's about what's on their screen, general knowledge, coding, math, science,
chess strategy, game tips, or anything else. Start with a short direct answer,
usually 1–3 sentences. Expand only when the user asks for detail."""

    history = get_conversation(conv_id)
    messages = [{'role': 'system', 'content': system_prompt}]
    for msg in history[-6:]:
        messages.append(msg)
    messages.append({'role': 'user', 'content': text})

    start_time = time.time()
    response_text = await query_ollama_chat(TEXT_MODEL, messages, on_chunk=on_chunk)
    elapsed = time.time() - start_time
    print(f"  Text query took {elapsed:.1f}s")

    add_to_conversation(conv_id, 'user', text)
    add_to_conversation(conv_id, 'assistant', response_text)

    return {
        'text': response_text,
        'highlights': None,
        'done': True,
        'is_streaming': False
    }


# ======================== PARSING ========================

# ======================== HEALTH CHECK ========================

async def check_ollama_health():
    """Verify Ollama is running and models are available."""
    async with httpx.AsyncClient(timeout=10.0) as client:
        try:
            resp = await client.get(f'{OLLAMA_BASE_URL}/api/tags')
            if resp.status_code == 200:
                models = resp.json().get('models', [])
                model_names = [m['name'] for m in models]
                print(f"  Available models: {', '.join(model_names) if model_names else 'None'}")

                if not any(VISION_MODEL.split(':')[0] in m for m in model_names):
                    print(f"\n  ⚠️  Vision model '{VISION_MODEL}' not found!")
                    print(f"  Run: ollama pull {VISION_MODEL}")

                if not any(TEXT_MODEL.split(':')[0] in m for m in model_names):
                    print(f"\n  ⚠️  Text model '{TEXT_MODEL}' not found!")
                    print(f"  Run: ollama pull {TEXT_MODEL}")

                return True
            return False
        except Exception:
            return False


# ======================== WEBSOCKET SERVER ========================

async def handle_client(websocket):
    """Handle one authenticated-protocol client and its cancellable requests."""
    addr = websocket.remote_address
    log_event("client.connected", remote_loopback=bool(addr and addr[0] in {"127.0.0.1", "::1"}))
    active_tasks: dict[str, asyncio.Task] = {}
    send_lock = asyncio.Lock()

    async def send_event(event: dict):
        async with send_lock:
            await websocket.send(json.dumps(event))

    async def run_request(request: dict):
        session_id = request['session_id']
        request_id = request['request_id']
        sequence = 1

        async def emit(event_type: str, **payload):
            nonlocal sequence
            await send_event(make_event(event_type, session_id, request_id, sequence, **payload))
            sequence += 1

        try:
            await ACCOUNT_GATE.require(request.get('account_token'))
            text = request.get('text') or ''
            audio_b64 = request.get('audio_base64')
            screenshot_b64 = request.get('screenshot_base64')
            active_window = request.get('active_window') or 'Unknown'
            screen_text = request.get('screen_text')

            log_event(
                "request.started",
                request_id=request_id,
                session_id=session_id,
                has_text=bool(text),
                has_audio=bool(audio_b64),
                has_screenshot=bool(screenshot_b64),
            )

            if audio_b64 and not text:
                transcription = await transcriber.transcribe_base64_wav(audio_b64)
                text = transcription.text
                await emit(
                    TRANSCRIPT_FINAL,
                    text=text,
                    language=transcription.language,
                    confidence=transcription.language_probability,
                )

            if not text and not screenshot_b64:
                raise ProtocolError("EMPTY_REQUEST", "Provide text, audio, or a screenshot.")

            fast_screen = use_screen_text(text, screen_text, active_window)
            guide = request.get('guide', False) or bool(audio_b64 and screenshot_b64 and guidance_requested(text))
            await emit(RESPONSE_STARTED, context_source='guide' if guide else 'screen_text' if fast_screen else 'vision' if screenshot_b64 else 'text')

            async def on_chunk(chunk: str):
                await emit(RESPONSE_DELTA, text=chunk)

            if guide:
                clicked = request.get('guide_clicked') or []
                failed = request.get('guide_failed') or []
                targets = eligible_targets(request.get('screen_targets') or [], clicked, failed)
                if targets:
                    step = direct_step(text, targets, clicked, request.get('guide_goal'))
                    if step is None and is_excel(active_window):
                        step = unsupported_excel_step()
                    if step is None:
                        answer = await query_ollama_chat(TEXT_MODEL, guide_messages(text, targets, clicked, failed, request.get('guide_goal'), active_window, screen_text))
                        step = parse_step(answer, targets, clicked, failed)
                else:
                    step = stalled_step()
                response = {'text': step['instruction'], 'highlights': None, 'guide': step}
            elif fast_screen:
                log_event('request.context', request_id=request_id, source='accessibility')
                answer = await query_ollama_chat(TEXT_MODEL, screen_messages(text, screen_text), on_chunk=on_chunk)
                response = {'text': answer, 'highlights': None}
            elif screenshot_b64:
                response = await process_vision_query(
                    text,
                    screenshot_b64,
                    active_window,
                    session_id,
                    on_chunk,
                )
            else:
                response = await process_text_query(text, active_window, session_id, on_chunk)

            log_event(
                "request.completed",
                request_id=request_id,
                session_id=session_id,
                response_characters=len(response['text']),
                highlight_count=len(response.get('highlights') or []),
            )
            await emit(
                RESPONSE_COMPLETED,
                text=response['text'],
                highlights=response.get('highlights'),
                capture=request.get('capture'),
                guide=response.get('guide'),
                done=True,
                is_streaming=False,
            )
        except asyncio.CancelledError:
            await emit(REQUEST_CANCELLED)
            raise
        except ProtocolError as error:
            log_event(
                "request.failed",
                request_id=request_id,
                session_id=session_id,
                error_code=error.code,
                retryable=error.retryable,
            )
            await send_event(make_error_event(session_id, request_id, sequence, error))
        except Exception as exc:
            log_event(
                "request.failed",
                request_id=request_id,
                session_id=session_id,
                error_code="INTERNAL_ERROR",
                exception_type=type(exc).__name__,
            )
            error = ProtocolError("INTERNAL_ERROR", "Whisper encountered an internal error.", True)
            await send_event(make_error_event(session_id, request_id, sequence, error))
        finally:
            active_tasks.pop(request_id, None)

    try:
        async for message in websocket:
            request = {}
            try:
                request = parse_client_envelope(json.loads(message), AUTH_TOKEN)
                request_id = request['request_id']

                if request['event_type'] == REQUEST_CANCEL:
                    task = active_tasks.get(request_id)
                    if task is not None:
                        task.cancel()
                    else:
                        await send_event(
                            make_event(
                                REQUEST_CANCELLED,
                                request['session_id'],
                                request_id,
                                1,
                            )
                        )
                    continue

                if request['event_type'] == REQUEST_START:
                    existing = active_tasks.get(request_id)
                    if existing is not None:
                        raise ProtocolError("DUPLICATE_REQUEST", "That request is already running.")
                    task = asyncio.create_task(run_request(request))
                    active_tasks[request_id] = task
            except (json.JSONDecodeError, RecursionError):
                await send_event(
                    make_error_event(
                        "unknown",
                        "unknown",
                        1,
                        ProtocolError("INVALID_JSON", "Request was not valid JSON."),
                    )
                )
            except ProtocolError as error:
                session_id = request.get('session_id', 'unknown') if isinstance(request, dict) else 'unknown'
                request_id = request.get('request_id', 'unknown') if isinstance(request, dict) else 'unknown'
                await send_event(make_error_event(session_id, request_id, 1, error))
    except websockets.exceptions.ConnectionClosed:
        log_event("client.disconnected")
    finally:
        tasks = list(active_tasks.values())
        for task in tasks:
            task.cancel()
        if tasks:
            await asyncio.gather(*tasks, return_exceptions=True)


async def main():
    # Windows redirected/background streams otherwise default to cp1252 and
    # crash on the Unicode startup banner before opening the listener.
    for output in (sys.stdout, sys.stderr):
        if hasattr(output, 'reconfigure'):
            output.reconfigure(encoding='utf-8', errors='backslashreplace')
    print(f"""
    ╔══════════════════════════════════════════════════════╗
    ║           WHISPER LOCAL ORCHESTRATOR v0.3            ║
    ║           VERSIONED STREAMING PROTOCOL               ║
    ║                                                      ║
    ║  Vision Model : {VISION_MODEL:<36s}║
    ║  Text Model   : {TEXT_MODEL:<36s}║
    ║  Ollama URL   : {OLLAMA_BASE_URL:<36s}║
    ║  WebSocket    : ws://localhost:{WEBSOCKET_PORT:<22}║
    ╚══════════════════════════════════════════════════════╝
    """)

    # Health check
    print("  Checking Ollama connection...")
    healthy = await check_ollama_health()
    if healthy:
        print("  ✅ Ollama is running!")
    else:
        print(f"  ❌ Cannot reach Ollama at {OLLAMA_BASE_URL}")
        print(f"  Make sure Ollama is installed and running:")
        print(f"    1. Download from https://ollama.ai/download")
        print(f"    2. Run: ollama serve")
        print(f"    3. Pull models: ollama pull {VISION_MODEL}")
        print(f"                    ollama pull {TEXT_MODEL}")
        print(f"\n  Starting server anyway (will retry on first query)...\n")

    print(f"\n  🟢 Whisper orchestrator is READY on ws://localhost:{WEBSOCKET_PORT}")
    print(f"  Local authentication token: {default_token_path()}")
    print(f"  Waiting for client connections...\n")

    async with websockets.serve(handle_client, "localhost", WEBSOCKET_PORT, max_size=MAX_MESSAGE_BYTES):
        await asyncio.Future()


if __name__ == '__main__':
    asyncio.run(main())
