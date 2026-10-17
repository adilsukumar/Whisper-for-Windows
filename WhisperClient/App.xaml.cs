using System.Windows;

namespace WhisperClient;

/// <summary>
/// Application entry point for the Whisper Cursor Assistant.
/// </summary>
public partial class App : Application
{
    public static WhisperClient.Core.DesktopAccountClient? AccountSession { get; private set; }
    private System.Net.Http.HttpClient? _accountsHttp;
    private System.Threading.Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstanceMutex = new System.Threading.Mutex(true, "WhisperCursorAssistant", out bool isNewInstance);
        if (!isNewInstance)
        {
            _singleInstanceMutex.Dispose(); _singleInstanceMutex = null;
            MessageBox.Show("Whisper is already running.", "Whisper"); Shutdown(); return;
        }
        try
        {
            var origin = WhisperClient.Core.ReleaseAccountPolicy.Origin;
            if (origin is not null)
            {
                _accountsHttp = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false })
                    { Timeout = TimeSpan.FromSeconds(10) };
                AccountSession = new(_accountsHttp, origin);
                if (new WhisperClient.UI.DesktopSignInWindow(AccountSession).ShowDialog() != true) { Shutdown(); return; }
            }
            MainWindow = new WhisperClient.UI.OverlayWindow();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow.Show();
        }
        catch (Exception)
        {
            MessageBox.Show("Whisper could not initialize. A paid release requires a configured HTTPS account service.", "Whisper");
            Shutdown();
        }

    }

    protected override void OnExit(ExitEventArgs e)
    {
        AccountSession = null;
        _accountsHttp?.Dispose();
        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }
        _singleInstanceMutex = null;
        base.OnExit(e);
    }
}
