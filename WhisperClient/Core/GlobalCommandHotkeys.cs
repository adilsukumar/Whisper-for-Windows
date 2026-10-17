using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WhisperClient.Core;

/// <summary>Help/quit remain available when capture hotkeys are paused.</summary>
public sealed class GlobalCommandHotkeys : IDisposable
{
    private const int HelpId = 0x77A1, QuitId = 0x77A2, WmHotkey = 0x0312;
    private const uint Modifiers = 0x0002 | 0x0004 | 0x4000; // Control, Shift, no repeat
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
    private readonly HwndSource _source;
    private readonly IntPtr _window;
    private bool _disposed;
    public bool HelpAvailable { get; }
    public bool QuitAvailable { get; }
    public event Action? HelpRequested;
    public event Action? QuitRequested;

    public GlobalCommandHotkeys(IntPtr window)
    {
        _window = window;
        _source = HwndSource.FromHwnd(window) ?? throw new InvalidOperationException("Window is not ready.");
        _source.AddHook(HandleMessage);
        HelpAvailable = RegisterHotKey(window, HelpId, Modifiers, 0x48); // H
        QuitAvailable = RegisterHotKey(window, QuitId, Modifiers, 0x51); // Q
    }

    private IntPtr HandleMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_disposed || message != WmHotkey) return IntPtr.Zero;
        if (wParam.ToInt32() == HelpId && HelpAvailable)
        {
            handled = true;
            _source.Dispatcher.BeginInvoke(() => { if (!_disposed) HelpRequested?.Invoke(); });
        }
        else if (wParam.ToInt32() == QuitId && QuitAvailable)
        {
            handled = true;
            _source.Dispatcher.BeginInvoke(() => { if (!_disposed) QuitRequested?.Invoke(); });
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (HelpAvailable) UnregisterHotKey(_window, HelpId);
        if (QuitAvailable) UnregisterHotKey(_window, QuitId);
        _source.RemoveHook(HandleMessage);
    }
}
