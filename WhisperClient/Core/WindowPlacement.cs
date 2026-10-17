namespace WhisperClient.Core;

/// <summary>Pure DIP-space layout helper. Monitor/DPI selection belongs to the caller.</summary>
public static class WindowPlacement
{
    public static (double X, double Y) Clamp(
        double x, double y, double width, double height,
        double left, double top, double availableWidth, double availableHeight)
    {
        if (new[] { x, y, width, height, left, top, availableWidth, availableHeight }
            .Any(value => !double.IsFinite(value)) ||
            width < 0 || height < 0 || availableWidth <= 0 || availableHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(availableWidth), "Layout values must be finite and bounds positive.");

        return (
            Math.Clamp(x, left, left + Math.Max(0, availableWidth - width)),
            Math.Clamp(y, top, top + Math.Max(0, availableHeight - height)));
    }
}
