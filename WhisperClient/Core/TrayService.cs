using Forms = System.Windows.Forms;

namespace WhisperClient.Core;

/// <summary>Windows-owned tray UI; all callbacks are marshalled by the overlay.</summary>
public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu = new();
    private readonly Forms.ToolStripMenuItem _ask = new("Ask Whisper…");
    private readonly Forms.ToolStripMenuItem _pause = new("Pause Whisper");
    private bool _paused;
    private bool _disposed;

    public event Action? AskRequested;
    public event Action? PauseToggleRequested;
    public event Action? QuitRequested;
    public event Action? SettingsRequested;
    public event Action? AccountRequested;
    public event Action? HelpRequested;

    public TrayService()
    {
        _ask.Click += (_, _) => { if (!_paused) AskRequested?.Invoke(); };
        _pause.Click += (_, _) => PauseToggleRequested?.Invoke();
        var quit = new Forms.ToolStripMenuItem("Quit Whisper (Ctrl+Shift+Q)");
        var help = new Forms.ToolStripMenuItem("Controls / Help (Ctrl+Shift+H)");
        help.Click += (_, _) => HelpRequested?.Invoke();
        var settings = new Forms.ToolStripMenuItem("Settings…");
        var account = new Forms.ToolStripMenuItem("Sign in / Switch account…");
        account.Click += (_, _) => AccountRequested?.Invoke();
        settings.Click += (_, _) => SettingsRequested?.Invoke();
        quit.Click += (_, _) => QuitRequested?.Invoke();
        _menu.Items.AddRange([_ask, _pause, settings, help, account, new Forms.ToolStripSeparator(), quit]);
        _icon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Whisper — Ready",
            ContextMenuStrip = _menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => { if (!_paused) AskRequested?.Invoke(); };
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        _ask.Enabled = !paused;
        _pause.Text = paused ? "Resume Whisper" : "Pause Whisper";
        _icon.Text = paused ? "Whisper — Paused" : "Whisper — Ready";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
