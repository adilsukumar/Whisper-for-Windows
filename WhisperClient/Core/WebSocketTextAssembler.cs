using System.IO;
using System.Text;

namespace WhisperClient.Core;

/// <summary>Decode complete UTF-8 messages, not individual network fragments.</summary>
public sealed class WebSocketTextAssembler : IDisposable
{
    public const int MaximumBytes = 512 * 1024;
    private readonly MemoryStream _buffer = new();
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public string? Append(ReadOnlySpan<byte> bytes, bool endOfMessage)
    {
        if (_buffer.Length + bytes.Length > MaximumBytes)
        {
            _buffer.SetLength(0);
            _buffer.Position = 0;
            throw new InvalidDataException("Orchestrator message exceeded the size limit.");
        }
        _buffer.Write(bytes);
        if (!endOfMessage) return null;
        try { return StrictUtf8.GetString(_buffer.GetBuffer(), 0, (int)_buffer.Length); }
        finally { _buffer.SetLength(0); _buffer.Position = 0; }
    }

    public void Dispose() => _buffer.Dispose();
}
