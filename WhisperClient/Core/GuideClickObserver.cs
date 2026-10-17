using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WhisperClient.Core;

/// <summary>One real click on one highlighted control; never sends input.</summary>
public sealed class GuideClickGate(PixelBounds target)
{
    private bool _armed, _finished;
    private long _downAt;
    private bool Inside(int x, int y) => x >= target.Left && y >= target.Top
        && (long)x < (long)target.Left + target.Width && (long)y < (long)target.Top + target.Height;
    public bool Observe(int message, int x, int y, bool matchingWindow, bool injected, long time)
    {
        if (_finished || injected) return false;
        if (message == 0x0201) { _armed = matchingWindow && Inside(x, y); _downAt = time; }
        if (message == 0x0200 && _armed && !Inside(x, y)) _armed = false;
        if (message != 0x0202) return false;
        var clicked = _armed && Inside(x, y) && time - _downAt is >= 0 and < 2000;
        _armed = false; _finished = clicked;
        return clicked;
    }
}

public sealed class GuideClickObserver : IDisposable
{
    private delegate IntPtr Callback(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Mouse { public Point Position; public uint MouseData, Flags, Time; public UIntPtr Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int type, Callback callback, IntPtr module, uint thread);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? module);
    private static readonly List<GuideClickObserver> FailedUnhooks = new();
    private readonly Callback _callback;
    private IntPtr _hook;
    private bool _disposed;

    public GuideClickObserver(ScreenTarget target, IntPtr window, Action clicked)
    {
        var gate = new GuideClickGate(new(target.X, target.Y, target.Width, target.Height));
        _callback = (code, message, data) =>
        {
            if (!_disposed && code >= 0)
            {
                var mouse = Marshal.PtrToStructure<Mouse>(data);
                if (gate.Observe((int)message, mouse.Position.X, mouse.Position.Y,
                    GetForegroundWindow() == window, (mouse.Flags & 1) != 0, Environment.TickCount64))
                {
                    // Queue work; never block the global input hook or swallow a click.
                    try { clicked(); } catch { }
                }
            }
            return CallNextHookEx(_hook, code, message, data);
        };
        _hook = SetWindowsHookEx(14, _callback, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hook != IntPtr.Zero && !UnhookWindowsHookEx(_hook))
            FailedUnhooks.Add(this); // Root disabled callbacks until process exit if Windows cannot unhook.
        _hook = IntPtr.Zero;
        GC.KeepAlive(_callback);
    }
}
