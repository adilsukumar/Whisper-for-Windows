using WhisperClient.Models;

namespace WhisperClient.Core;

public static class WhisperRequestFactory
{
    public static WhisperRequest CreateWithCapture(string? text, string? audio, bool includeScreen,
        Func<ScreenSnapshot> captureScreen, Func<string> activeWindow)
    {
        var capture = includeScreen ? captureScreen() : null;
        return new WhisperRequest { Text = text, AudioBase64 = audio, ScreenshotBase64 = capture?.Base64,
            Capture = capture?.Descriptor, ActiveWindow = includeScreen ? activeWindow() : "Unknown" };
    }
    public static WhisperRequest Create(string? text, string? audio, bool includeScreen,
        Func<string> captureScreen, Func<string> activeWindow) => new()
    {
        Text = text,
        AudioBase64 = audio,
        ScreenshotBase64 = includeScreen ? captureScreen() : null,
        ActiveWindow = includeScreen ? activeWindow() : "Unknown"
    };
}
