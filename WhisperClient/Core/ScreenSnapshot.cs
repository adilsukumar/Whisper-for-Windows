using System.Text.Json.Serialization;

namespace WhisperClient.Core;

public sealed record CaptureDescriptor
{
    [JsonPropertyName("capture_id")] public string CaptureId { get; init; } = Guid.NewGuid().ToString();
    [JsonPropertyName("monitor_id")] public string MonitorId { get; init; } = "";
    [JsonPropertyName("origin_x")] public int OriginX { get; init; }
    [JsonPropertyName("origin_y")] public int OriginY { get; init; }
    [JsonPropertyName("width")] public int Width { get; init; }
    [JsonPropertyName("height")] public int Height { get; init; }
    [JsonPropertyName("coordinate_space")] public string CoordinateSpace { get; init; } = "capture_pixels";
    [JsonIgnore] public PixelBounds Bounds => new(OriginX, OriginY, Width, Height);
    // Local-only mapping: wire bounds remain the physical display for capture identity.
    [JsonIgnore] public int PreviewWidth { get; init; }
    [JsonIgnore] public int PreviewHeight { get; init; }
    [JsonIgnore] public double PreviewScaleX => PreviewWidth > 0 ? (double)Width / PreviewWidth : 1;
    [JsonIgnore] public double PreviewScaleY => PreviewHeight > 0 ? (double)Height / PreviewHeight : 1;
}

public sealed record ScreenSnapshot(string Base64, CaptureDescriptor Descriptor);
