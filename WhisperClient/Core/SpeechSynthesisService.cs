using System.Speech.Synthesis;
using System.Text.RegularExpressions;

namespace WhisperClient.Core;

/// <summary>
/// Interruptible, offline Windows TTS fallback. Cloud/neural voices can replace
/// this adapter later without changing overlay lifecycle behavior.
/// </summary>
public sealed partial class SpeechSynthesisService : IDisposable
{
    private readonly SpeechSynthesizer _synthesizer = new();
    private bool _disposed;

    public event Action? SpeechCompleted;

    public SpeechSynthesisService()
    {
        _synthesizer.Rate = 1;
        _synthesizer.Volume = 100;
        _synthesizer.SpeakCompleted += (_, args) =>
        {
            if (!args.Cancelled)
                SpeechCompleted?.Invoke();
        };
    }

    public bool Speak(string text)
    {
        ThrowIfDisposed();
        Cancel();

        var speakableText = PrepareForSpeech(text);
        if (string.IsNullOrWhiteSpace(speakableText))
            return false;

        try
        {
            _synthesizer.SpeakAsync(speakableText);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public void Cancel()
    {
        if (!_disposed)
            _synthesizer.SpeakAsyncCancelAll();
    }

    internal static string PrepareForSpeech(string text)
    {
        var withoutCodeBlocks = CodeBlockRegex().Replace(text, " Code block omitted. ");
        var withoutLinks = MarkdownLinkRegex().Replace(withoutCodeBlocks, "$1");
        var withoutMarkdown = MarkdownMarkerRegex().Replace(withoutLinks, "");
        var normalized = WhitespaceRegex().Replace(withoutMarkdown, " ").Trim();
        return normalized.Length <= 4_000 ? normalized : normalized[..4_000];
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _synthesizer.SpeakAsyncCancelAll();
        _synthesizer.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    [GeneratedRegex("```[\\s\\S]*?```", RegexOptions.Compiled)]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex("\\[([^\\]]+)\\]\\([^)]+\\)", RegexOptions.Compiled)]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex("[`*_#>]", RegexOptions.Compiled)]
    private static partial Regex MarkdownMarkerRegex();

    [GeneratedRegex("\\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
