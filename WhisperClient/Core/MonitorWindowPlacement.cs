using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WhisperClient.Core;

/// <summary>Place a native HWND using physical pixels in the cursor monitor's work area.</summary>
public static class MonitorWindowPlacement
{
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo
    { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr window, IntPtr after,
        int x, int y, int width, int height, uint flags);

    public static void Place(IntPtr window, int anchorX, int anchorY)
    {
        var monitor = MonitorFromPoint(new Point { X = anchorX, Y = anchorY }, 2); // nearest
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info) || !GetWindowRect(window, out var rect))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        // First move switches monitor DPI. Re-read the resulting native size before clamping.
        if (!SetWindowPos(window, IntPtr.Zero, anchorX, anchorY, 0, 0, 0x0015))
            throw new Win32Exception(Marshal.GetLastWin32Error()); // NOSIZE | NOZORDER | NOACTIVATE
        if (!GetWindowRect(window, out rect)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var width = (double)rect.Right - rect.Left;
        var height = (double)rect.Bottom - rect.Top;
        var position = WindowPlacement.Clamp(anchorX - width / 2, (double)anchorY + 30,
            width, height, info.Work.Left, info.Work.Top,
            (double)info.Work.Right - info.Work.Left, (double)info.Work.Bottom - info.Work.Top);
        if (!SetWindowPos(window, IntPtr.Zero, (int)position.X, (int)position.Y, 0, 0, 0x0015))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
}
