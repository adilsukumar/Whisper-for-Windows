using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using WhisperClient.Models;

namespace WhisperClient.Core;

/// <summary>
/// WebSocket client for real-time bidirectional communication with the Whisper Cloud Brain.
/// Supports streaming responses so text appears word-by-word as the LLM generates it.
/// </summary>
public sealed class BrainClient : IDisposable
{
    private readonly string _serverUrl;
    private readonly string _sessionId = Guid.NewGuid().ToString();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly LocalAuthTokenProvider _authTokenProvider;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _lifetimeCts;
    private Task? _receiveTask;
    private string? _activeRequestId;
    private bool _disposed;

    public event Action<string>? OnTextChunk;
    public event Action<string>? OnContextSource;
    public event Action<string>? OnTranscript;
    public event Action<WhisperResponse>? OnFullResponse;
    public event Action<string>? OnError;
    public event Action? OnConnected;
    public event Action? OnDisconnected;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;
    public string SessionId => _sessionId;
    public string? ActiveRequestId => _activeRequestId;

    public BrainClient(string serverUrl = "ws://localhost:8765", LocalAuthTokenProvider? authTokenProvider = null)
    {
        _serverUrl = serverUrl;
        _authTokenProvider = authTokenProvider ?? new LocalAuthTokenProvider();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            return;

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
                return;

            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
            _webSocket?.Dispose();

            _lifetimeCts = new CancellationTokenSource();
            Exception? lastError = null;
            int[] retryDelaysMs = [250, 750, 1_500];

            for (var attempt = 0; attempt <= retryDelaysMs.Length; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var socket = new ClientWebSocket();
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                connectCts.CancelAfter(TimeSpan.FromSeconds(5));

                try
                {
                    await socket.ConnectAsync(new Uri(_serverUrl), connectCts.Token);
                    _webSocket = socket;
                    lastError = null;
                    break;
                }
                catch (Exception error) when (error is WebSocketException or OperationCanceledException)
                {
                    lastError = error;
                    socket.Dispose();
                    if (attempt < retryDelaysMs.Length)
                        await Task.Delay(retryDelaysMs[attempt], cancellationToken);
                }
            }

            if (_webSocket?.State != WebSocketState.Open)
                throw new WebSocketException(lastError?.Message ?? "Connection attempts failed.");

            _receiveTask = ReceiveLoop(_webSocket, _lifetimeCts.Token);
            OnConnected?.Invoke();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            OnError?.Invoke($"Connection failed: {ex.Message}");
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <summary>
    /// Sends a new turn and returns its request ID. A newer turn supersedes the
    /// prior active request, which is cancelled before the new request is sent.
    /// </summary>
    public async Task<string?> SendQueryAsync(WhisperRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            await ConnectAsync(cancellationToken);

        if (!IsConnected)
        {
            OnError?.Invoke("Not connected to the Whisper orchestrator.");
            return null;
        }

        if (_activeRequestId is not null)
            await CancelActiveRequestAsync(cancellationToken);

        request.ProtocolVersion = WhisperProtocol.Version;
        request.EventType = WhisperProtocol.RequestStart;
        request.SessionId = _sessionId;
        request.RequestId = Guid.NewGuid().ToString();
        request.Sequence = 1;
        request.Timestamp = DateTimeOffset.UtcNow.ToString("O");
        _activeRequestId = request.RequestId;

        try
        {
            request.AuthToken = _authTokenProvider.GetToken();
            await SendEnvelopeAsync(request, cancellationToken);
            return request.RequestId;
        }
        catch (OperationCanceledException)
        {
            if (_activeRequestId == request.RequestId)
                _activeRequestId = null;
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _activeRequestId = null;
            OnError?.Invoke($"Send failed: {ex.Message}");
            return null;
        }
    }

    public async Task CancelActiveRequestAsync(CancellationToken cancellationToken = default)
    {
        var requestId = _activeRequestId;
        // Detach synchronously: late events must not reach a newly opened input
        // window, and completion of this send must not clear a newer request ID.
        _activeRequestId = null;
        if (requestId is null || !IsConnected)
            return;

        try
        {
            var cancel = new WhisperRequest
            {
                EventType = WhisperProtocol.RequestCancel,
                SessionId = _sessionId,
                RequestId = requestId,
                Sequence = 1,
                Timestamp = DateTimeOffset.UtcNow.ToString("O"),
                AuthToken = _authTokenProvider.GetToken()
            };
            await SendEnvelopeAsync(cancel, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            OnError?.Invoke($"Cancellation failed: {ex.Message}");
        }
    }

    private async Task SendEnvelopeAsync(WhisperRequest request, CancellationToken cancellationToken)
    {
        if (_webSocket?.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket is not open.");

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReceiveLoop(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var assembler = new WebSocketTextAssembler();

        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType != WebSocketMessageType.Text)
                    throw new System.IO.InvalidDataException("Expected a text protocol event.");
                var fullMessage = assembler.Append(buffer.AsSpan(0, result.Count), result.EndOfMessage);
                if (fullMessage is null) continue;
                HandleIncomingMessage(fullMessage);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown or request cancellation.
        }
        catch (Exception)
        {
            if (!cancellationToken.IsCancellationRequested && ReferenceEquals(_webSocket, socket))
                OnError?.Invoke("The orchestrator connection was interrupted or returned invalid data. Try again.");
        }
        finally
        {
            // Do not leave an apparently open socket after its receive loop has failed.
            // This loop owns only its original socket, never a newer connection.
            try { socket.Abort(); } catch (ObjectDisposedException) { }
            if (ReferenceEquals(_webSocket, socket)) OnDisconnected?.Invoke();
        }
    }

    private void HandleIncomingMessage(string message)
    {
        try
        {
            var response = JsonSerializer.Deserialize<WhisperResponse>(message);
            if (response is null)
                return;

            // Ignore late events from requests superseded by a newer turn.
            if (!string.IsNullOrEmpty(response.RequestId))
            {
                if (_activeRequestId is null || response.RequestId != _activeRequestId)
                    return;
            }

            switch (response.EventType)
            {
                case WhisperProtocol.TranscriptFinal:
                    OnTranscript?.Invoke(response.Text);
                    break;
                case WhisperProtocol.ResponseStarted:
                    OnContextSource?.Invoke(response.ContextSource ?? "text");
                    break;
                case WhisperProtocol.ResponseDelta:
                    OnTextChunk?.Invoke(response.Text);
                    break;
                case WhisperProtocol.ResponseCompleted:
                    _activeRequestId = null;
                    OnFullResponse?.Invoke(response);
                    break;
                case WhisperProtocol.RequestCancelled:
                    _activeRequestId = null;
                    break;
                case WhisperProtocol.Error:
                    _activeRequestId = null;
                    OnError?.Invoke(response.Error?.Message ?? "Whisper encountered an unknown error.");
                    break;
                default:
                    // Backward compatibility with the prototype response shape.
                    if (response.IsStreaming && !response.Done)
                        OnTextChunk?.Invoke(response.Text);
                    else
                    {
                        _activeRequestId = null;
                        OnFullResponse?.Invoke(response);
                    }
                    break;
            }
        }
        catch (JsonException)
        {
            OnError?.Invoke("The orchestrator returned an invalid response.");
        }
    }

    public async Task DisconnectAsync()
    {
        _activeRequestId = null;
        _lifetimeCts?.Cancel();
        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client closing",
                    CancellationToken.None);
            }
            catch (WebSocketException)
            {
                // The remote endpoint may already be unavailable.
            }
        }

        if (_receiveTask is not null)
        {
            try { await _receiveTask; }
            catch (OperationCanceledException) { }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _lifetimeCts?.Cancel();
        _webSocket?.Dispose();
        _lifetimeCts?.Dispose();
        _sendLock.Dispose();
        _connectLock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
