using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace WhisperClient.Core;

public sealed record ScreenTarget
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("label")] public string Label { get; init; } = "";
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonIgnore] public string RuntimeId { get; init; } = "";
    [JsonIgnore] public int Priority { get; init; }
    [JsonIgnore] public int X { get; init; }
    [JsonIgnore] public int Y { get; init; }
    [JsonIgnore] public int Width { get; init; }
    [JsonIgnore] public int Height { get; init; }
}

public sealed record ScreenTextSnapshot(string Text, IReadOnlyList<ScreenTarget> Targets);
public sealed record GuideStep
{
    [JsonPropertyName("target_id")] public string? TargetId { get; init; }
    [JsonPropertyName("instruction")] public string Instruction { get; init; } = "";
    [JsonPropertyName("done")] public bool Done { get; init; }
}

public static class Guidance
{
    public static bool CanOffer(string label, IEnumerable<string> clicked, IEnumerable<string> failed)
    {
        static string Normalize(string value) => Regex.Replace(value.Trim(), @"\s+", " ");
        var normalized = Normalize(label);
        return !clicked.Concat(failed).Any(previous => string.Equals(Normalize(previous), normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static bool Requested(string question) => Regex.IsMatch(question,
        @"\b(guide|walk(?: me)? through|show me|point(?: me)? to|where (?:is|do i click)|open|go to|click|select|locate)\b", RegexOptions.IgnoreCase);

    public static ScreenTarget? Resolve(ScreenTarget original, IReadOnlyList<ScreenTarget> fresh, PixelBounds monitor)
    {
        if (string.IsNullOrEmpty(original.RuntimeId)) return null;
        var matches = fresh.Where(target => target.RuntimeId == original.RuntimeId && target.Label == original.Label && target.State == original.State
            && target.Width > 0 && target.Height > 0 && target.X >= monitor.Left && target.Y >= monitor.Top
            && (long)target.X + target.Width <= (long)monitor.Left + monitor.Width
            && (long)target.Y + target.Height <= (long)monitor.Top + monitor.Height).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
}
