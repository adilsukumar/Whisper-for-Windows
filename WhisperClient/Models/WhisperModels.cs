using System.Text.Json.Serialization;

namespace WhisperClient.Models;

public static class WhisperProtocol
{
    public const string Version = "1.0";
    public const string RequestStart = "request.start";
    public const string RequestCancel = "request.cancel";
    public const string TranscriptFinal = "transcript.final";
    public const string ResponseStarted = "response.started";
    public const string ResponseDelta = "response.delta";
    public const string ResponseCompleted = "response.completed";
    public const string RequestCancelled = "request.cancelled";
    public const string Error = "error";
}

/// <summary>
/// Versioned request envelope. SessionId remains stable for the BrainClient lifetime;
/// RequestId is unique for every turn.
/// </summary>
public sealed class WhisperRequest
{
    [JsonPropertyName("protocol_version")]
    public string ProtocolVersion { get; set; } = WhisperProtocol.Version;

    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = WhisperProtocol.RequestStart;

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = "";

    [JsonPropertyName("sequence")]
    public long Sequence { get; set; } = 1;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTimeOffset.UtcNow.ToString("O");

    [JsonPropertyName("auth_token")]
    public string AuthToken { get; set; } = "";
    [JsonPropertyName("account_token")]
    public string? AccountToken { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("audio_base64")]
    public string? AudioBase64 { get; set; }

    [JsonPropertyName("screenshot_base64")]
    public string? ScreenshotBase64 { get; set; }
    [JsonPropertyName("screen_text")]
    public string? ScreenText { get; set; }
    [JsonPropertyName("screen_targets")]
    public IReadOnlyList<WhisperClient.Core.ScreenTarget>? ScreenTargets { get; set; }
    [JsonPropertyName("guide")]
    public bool Guide { get; set; }
    [JsonPropertyName("guide_goal")]
    public string? GuideGoal { get; set; }
    [JsonPropertyName("guide_clicked")]
    public IReadOnlyList<string>? GuideClicked { get; set; }
    [JsonPropertyName("guide_failed")]
    public IReadOnlyList<string>? GuideFailed { get; set; }

    [JsonPropertyName("active_window")]
    public string? ActiveWindow { get; set; }
    [JsonPropertyName("capture")]
    public WhisperClient.Core.CaptureDescriptor? Capture { get; set; }
}

public sealed class WhisperResponse
{
    [JsonPropertyName("guide")]
    public WhisperClient.Core.GuideStep? Guide { get; set; }
    [JsonPropertyName("context_source")]
    public string? ContextSource { get; set; }
    [JsonPropertyName("capture")]
    public WhisperClient.Core.CaptureDescriptor? Capture { get; set; }
    [JsonPropertyName("protocol_version")]
    public string ProtocolVersion { get; set; } = WhisperProtocol.Version;

    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = WhisperProtocol.ResponseCompleted;

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = "";

    [JsonPropertyName("sequence")]
    public long Sequence { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }

    [JsonPropertyName("audio_base64")]
    public string? AudioBase64 { get; set; }

    [JsonPropertyName("highlights")]
    public List<ScreenHighlight>? Highlights { get; set; }

    [JsonPropertyName("error")]
    public WhisperError? Error { get; set; }

    // Legacy compatibility while the protocol migrates.
    [JsonPropertyName("is_streaming")]
    public bool IsStreaming { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

public sealed class WhisperError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "INTERNAL_ERROR";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "Whisper encountered an unexpected error.";

    [JsonPropertyName("retryable")]
    public bool Retryable { get; set; }
}

public sealed class ScreenHighlight
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "box";
}
