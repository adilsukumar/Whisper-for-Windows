namespace WhisperClient.Core;

/// <summary>Owns turn cancellation and the local pause gate; resume never starts capture.</summary>
public sealed class InteractionLifetime : IDisposable
{
    private CancellationTokenSource? _turn;
    private bool _disposed;
    public bool IsPaused { get; private set; }
    public CancellationToken CurrentToken => _turn?.Token ?? new CancellationToken(true);

    public bool TryBeginTurn(out CancellationToken token)
    {
        token = new CancellationToken(true);
        if (_disposed || IsPaused) return false;
        CancelTurn();
        _turn?.Dispose();
        _turn = new CancellationTokenSource();
        token = _turn.Token;
        return true;
    }

    public void CancelTurn() => _turn?.Cancel();

    public bool Pause()
    {
        if (_disposed || IsPaused) return false;
        IsPaused = true;
        CancelTurn();
        return true;
    }

    public bool Resume()
    {
        if (_disposed || !IsPaused) return false;
        IsPaused = false;
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        IsPaused = true;
        CancelTurn();
        _turn?.Dispose();
        _turn = null;
    }
}
