namespace WhisperClient.Core;

/// <summary>Bound CPU vision work while retaining the physical monitor mapping.</summary>
public static class CapturePreviewGeometry
{
    public static (int Width, int Height) Size(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var scale = Math.Min(1.0, 640.0 / Math.Max(width, height));
        return (Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)));
    }
}
