namespace WhisperClient.Core;

/// <summary>Reject invalid model geometry; clip valid boxes to the visible canvas.</summary>
public static class HighlightGeometry
{
    public static (double X, double Y, double Width, double Height)? ClipBox(
        double x, double y, double width, double height, double canvasWidth, double canvasHeight)
    {
        if (new[] { x, y, width, height, canvasWidth, canvasHeight }.Any(v => !double.IsFinite(v))
            || width <= 0 || height <= 0 || canvasWidth <= 0 || canvasHeight <= 0) return null;
        var right = Math.Min(canvasWidth, x + width);
        var bottom = Math.Min(canvasHeight, y + height);
        x = Math.Max(0, x);
        y = Math.Max(0, y);
        return right > x && bottom > y ? (x, y, right - x, bottom - y) : null;
    }
}
