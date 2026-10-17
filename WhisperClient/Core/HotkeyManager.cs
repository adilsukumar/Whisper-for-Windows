using System.Runtime.InteropServices;

namespace WhisperClient.Core;

/// <summary>
/// Low-level global keyboard hook that captures hotkeys even when app is not focused.
/// Uses Win32 SetWindowsHookEx for system-wide keyboard interception.
/// </summary>
public class HotkeyManager : IDisposable
{
    // Win32 API declarations
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;

    // Virtual key codes
    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT = 0x10;
    private const int VK_SPACE = 0x20;
    private const int VK_ESCAPE = 0x1B;

    private IntPtr _hookId = IntPtr.Zero;
    private readonly LowLevelKeyboardProc _proc;

    // Events
    public event Action? VoiceModeActivated;    // Ctrl+Space pressed
    public event Action? VoiceModeDeactivated;  // Ctrl+Space released
    public event Action? TextModeToggled;       // Ctrl+Shift+Space pressed
    public event Action? DismissRequested;      // Escape pressed

    private bool _isVoiceModeActive = false;
    private bool _isTextKeyDown;
    private bool _enabled;
    private bool _disposed = false;

    public HotkeyManager()
    {
        _proc = HookCallback;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_hookId != IntPtr.Zero) { _enabled = true; return; }
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(module.ModuleName), 0);

        if (_hookId == IntPtr.Zero)
            throw new InvalidOperationException("Failed to install keyboard hook.");
        _enabled = true;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (_enabled && nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            bool ctrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            bool shiftDown = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;

            if (wParam == (IntPtr)WM_KEYDOWN)
            {
                if (vkCode == VK_SPACE && _isTextKeyDown)
                    return (IntPtr)1;
                // Ctrl+Shift+Space → Text Mode
                if (vkCode == VK_SPACE && ctrlDown && shiftDown)
                {
                    _isTextKeyDown = true;
                    TextModeToggled?.Invoke();
                    return (IntPtr)1; // Consume the key
                }
                // Ctrl+Space → Voice Mode (push-to-talk START)
                else if (vkCode == VK_SPACE && ctrlDown && !shiftDown)
                {
                    if (!_isVoiceModeActive)
                    {
                        _isVoiceModeActive = true;
                        VoiceModeActivated?.Invoke();
                    }
                    return (IntPtr)1;
                }
                // Escape → Dismiss
                else if (vkCode == VK_ESCAPE)
                {
                    DismissRequested?.Invoke();
                }
            }
            else if (wParam == (IntPtr)WM_KEYUP)
            {
                if (vkCode == VK_SPACE && _isTextKeyDown)
                {
                    _isTextKeyDown = false;
                    return (IntPtr)1;
                }
                // Ctrl+Space released → Voice Mode (push-to-talk STOP)
                if (vkCode == VK_SPACE && _isVoiceModeActive)
                {
                    _isVoiceModeActive = false;
                    VoiceModeDeactivated?.Invoke();
                    return (IntPtr)1;
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    public void Stop()
    {
        // Gate callbacks before unhooking: even an OS unhook failure cannot
        // trigger capture or consume keys while the assistant is paused.
        _enabled = false;
        if (_hookId != IntPtr.Zero && UnhookWindowsHookEx(_hookId))
            _hookId = IntPtr.Zero;
        _isVoiceModeActive = false;
        _isTextKeyDown = false;
    }
}
