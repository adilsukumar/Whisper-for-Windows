using System.Runtime.InteropServices;
using System.Text;

namespace WhisperClient.Services;

/// <summary>
/// Detects the currently active (foreground) window title and process name.
/// This is used to inject app-specific context into the LLM system prompt,
/// making Whisper an expert in whatever application you're currently using.
/// </summary>
public static class AppDetectionService
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    public static IntPtr GetActiveWindowHandle() => GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>
    /// Returns the title of the currently focused window.
    /// </summary>
    public static string GetActiveWindowTitle()
    {
        var buffer = new StringBuilder(512);
        IntPtr handle = GetForegroundWindow();

        if (GetWindowText(handle, buffer, buffer.Capacity) > 0)
            return buffer.ToString();

        return "Unknown";
    }

    /// <summary>
    /// Returns the process name of the currently focused window.
    /// </summary>
    public static string GetActiveProcessName()
    {
        try
        {
            IntPtr handle = GetForegroundWindow();
            GetWindowThreadProcessId(handle, out uint processId);
            var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <summary>
    /// Returns a combined string with both window title and process for context.
    /// </summary>
    public static string GetActiveWindowInfo()
    {
        return $"{GetActiveProcessName()} - {GetActiveWindowTitle()}";
    }
}
