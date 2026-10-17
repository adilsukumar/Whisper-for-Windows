namespace WhisperClient.Core;

public static class CaptureBounds
{
    public static (int X, int Y, int Width, int Height) PrimaryRegion(
        int centerX, int centerY, int requestedWidth, int requestedHeight, int screenWidth, int screenHeight)
    {
        if (requestedWidth <= 0 || requestedHeight <= 0 || screenWidth <= 0 || screenHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestedWidth));
        var width = Math.Min(requestedWidth, screenWidth);
        var height = Math.Min(requestedHeight, screenHeight);
        return ((int)Math.Clamp((double)centerX - width / 2.0, 0, screenWidth - width),
            (int)Math.Clamp((double)centerY - height / 2.0, 0, screenHeight - height), width, height);
    }
}
