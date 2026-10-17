using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WhisperClient.Core;

public sealed record PixelBounds(int Left, int Top, int Width, int Height);
public sealed record DesktopMonitor(string DeviceName, PixelBounds Bounds, PixelBounds WorkArea);

public static class DisplayTopology
{
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Info
    { public int Size; public Rect Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetMonitorInfo(IntPtr monitor, ref Info info);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();

    public static DesktopMonitor At(int x, int y)
    {
        var handle = MonitorFromPoint(new Point { X = x, Y = y }, 2);
        var info = new Info { Size = Marshal.SizeOf<Info>(), Device = "" };
        if (!GetMonitorInfo(handle, ref info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        static PixelBounds Bounds(Rect value) => new(value.Left, value.Top, value.Right - value.Left, value.Bottom - value.Top);
        return new(info.Device, Bounds(info.Monitor), Bounds(info.Work));
    }

    public static void FitWindow(IntPtr window, PixelBounds bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) throw new ArgumentOutOfRangeException(nameof(bounds));
        // Moving may synchronously trigger a DPI resize. Apply physical bounds again afterwards.
        for (var pass = 0; pass < 2; pass++)
            if (!SetWindowPos(window, IntPtr.Zero, bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x0014))
                throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public static bool ExcludeFromCapture(IntPtr window) => SetWindowDisplayAffinity(window, 0x11);
    public static void FlushComposition()
    {
        if (DwmFlush() < 0) throw new InvalidOperationException("Cannot synchronize screen composition.");
    }
}
