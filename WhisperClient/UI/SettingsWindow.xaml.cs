using System.Windows;
using WhisperClient.Core;

namespace WhisperClient.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _store;
    private readonly Func<bool> _brainConnected;
    private readonly CancellationTokenSource _lifetime = new();
    public event Action<UserSettings>? Saved;

    public SettingsWindow(SettingsStore store, UserSettings settings, string? warning, Func<bool> brainConnected)
    {
        InitializeComponent();
        _store = store;
        _brainConnected = brainConnected;
        Closed += (_, _) => _lifetime.Cancel();
        SpeakResponses.IsChecked = settings.SpeakResponses;
        VoiceScreen.IsChecked = settings.IncludeScreenForVoice;
        ReducedMotion.IsChecked = settings.ReducedMotion;
        Status.Text = warning ?? "Settings are saved locally; no screenshots, transcripts, or keys are stored here.";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var settings = new UserSettings
        {
            SpeakResponses = SpeakResponses.IsChecked == true,
            IncludeScreenForVoice = VoiceScreen.IsChecked == true,
            ReducedMotion = ReducedMotion.IsChecked == true
        };
        try { _store.Save(settings); }
        catch (InvalidOperationException error) { Status.Text = error.Message; return; }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        {
            Status.Text = "Could not save settings. Existing settings have not been applied or discarded.";
            return;
        }
        Saved?.Invoke(settings);
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private async void OnCheckRuntime(object sender, RoutedEventArgs e)
    {
        CheckRuntime.IsEnabled = false;
        RuntimeStatus.Text = "Checking local services...";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var client = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler { UseProxy = false, AllowAutoRedirect = false });
        try
        {
            var result = await RuntimeReadiness.CheckAsync(client,
                Environment.GetEnvironmentVariable("WHISPER_TEXT_MODEL") ?? "qwen3-vl:2b-instruct-q4_K_M",
                Environment.GetEnvironmentVariable("WHISPER_VISION_MODEL") ?? "qwen3-vl:2b-instruct-q4_K_M", timeout.Token);
            if (_lifetime.IsCancellationRequested) return;
            RuntimeStatus.Text = $"Local brain: {(_brainConnected() ? "connected" : "not connected")}\n"
                + $"Text model: {(result.TextInstalled ? "installed" : "missing/unavailable")}\n"
                + $"Vision model: {(result.VisionInstalled ? "installed" : "missing/unavailable")}\n{result.Message}";
        }
        catch (OperationCanceledException)
        {
            if (!_lifetime.IsCancellationRequested) RuntimeStatus.Text = "Runtime check timed out. No download was started.";
        }
        finally { if (!_lifetime.IsCancellationRequested) CheckRuntime.IsEnabled = true; }
    }
}
