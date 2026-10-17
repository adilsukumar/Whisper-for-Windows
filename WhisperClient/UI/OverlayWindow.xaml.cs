using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WhisperClient.Core;
using WhisperClient.Models;
using WhisperClient.Services;

namespace WhisperClient.UI;

public partial class OverlayWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewValue);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private readonly HotkeyManager _hotkeyManager;
    private readonly ScreenCaptureService _screenCapture;
    private readonly AudioRecorderService _audioRecorder;
    private readonly BrainClient _brainClient;
    private readonly SpeechSynthesisService _speechSynthesis;
    private readonly WhisperSessionStateMachine _stateMachine = new();
    private readonly InteractionLifetime _interactions = new();
    private TrayService? _tray;
    private readonly SettingsStore _settingsStore = new();
    private UserSettings _settings;
    private string? _settingsWarning;
    private SettingsWindow? _settingsWindow;
    private ShortcutHelpWindow? _helpWindow;
    private GlobalCommandHotkeys? _commandHotkeys;
    private bool _voiceScreenForTurn;
    private bool _screenSharedForTurn;
    private DesktopMonitor? _cursorMonitor;
    private DesktopMonitor? _queryMonitor;
    private DesktopMonitor? _voiceMonitor;
    private CaptureDescriptor? _captureForTurn;
    private AnnotationWindow? _annotations;
    private bool _voiceKeyHeld;
    private IntPtr _queryWindowHandle;
    private IntPtr _voiceWindowHandle;
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    private IntPtr _turnWindowHandle;
    private DesktopMonitor? _turnMonitor;
    private ScreenTextSnapshot? _turnScreenText;
    private WalkthroughWindow? _walkthrough;
    private string? _walkthroughGoal;
    private string? _walkthroughInstruction;
    private readonly List<string> _walkthroughCompleted = new();
    private bool _continuingWalkthrough;
    private GuideClickObserver? _guideClickObserver;
    private bool _guideAdvancing;
    private readonly List<string> _walkthroughClickedLabels = new();
    private readonly List<string> _walkthroughFailedLabels = new();
    private string? _walkthroughTargetLabel;
    private bool _observedGuideClickPending;
    private async Task RequireAccountAsync(CancellationToken cancellation)
    {
        if (App.AccountSession is not null) await App.AccountSession.RequireAccessAsync(cancellation);
    }

    private double _blobX = 0, _blobY = 0;
    private double _targetX = 0, _targetY = 0;
    private double _breathPhase = 0;
    private readonly DispatcherTimer _animationTimer;
    private readonly DispatcherTimer _cursorTracker;
    private DispatcherTimer? _dismissTimer;

    private string _currentResponse = "";
    private readonly List<UIElement> _highlightElements = new();
    private QueryInputWindow? _queryWindow;
    private string _queryActiveWindow = "Unknown";

    public OverlayWindow()
    {
        InitializeComponent();
        (_settings, _settingsWarning) = _settingsStore.Load();

        _hotkeyManager = new HotkeyManager();
        _screenCapture = new ScreenCaptureService();
        _audioRecorder = new AudioRecorderService();
        _brainClient = new BrainClient("ws://localhost:8765");
        _speechSynthesis = new SpeechSynthesisService();

        _hotkeyManager.VoiceModeActivated += OnVoiceModeStart;
        _hotkeyManager.VoiceModeDeactivated += OnVoiceModeStop;
        _hotkeyManager.TextModeToggled += OnTextModeToggle;
        _hotkeyManager.DismissRequested += OnDismiss;

        _brainClient.OnTextChunk += OnBrainTextChunk;
        _brainClient.OnContextSource += source => Dispatcher.Invoke(() =>
        {
            if (_stateMachine.Current != WhisperState.Thinking) return;
            StatusText.Text = source == "guide" ? "Finding the next control…" : source == "screen_text" ? "Reading screen text…"
                : source == "vision" ? "Analyzing image (slower)…" : "Answering…";
            StatusText.Visibility = Visibility.Visible;
        });
        _brainClient.OnTranscript += OnBrainTranscript;
        _brainClient.OnFullResponse += OnBrainFullResponse;
        _brainClient.OnError += OnBrainError;
        _speechSynthesis.SpeechCompleted += OnSpeechCompleted;

        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animationTimer.Tick += AnimationTick;

        _cursorTracker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _cursorTracker.Tick += CursorTrackerTick;

        Loaded += OnWindowLoaded;
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        MakeOverlayClickThrough();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _hotkeyManager.Start();
        _animationTimer.Start();
        _cursorTracker.Start();
        _tray = new TrayService();
        _tray.AskRequested += () => Dispatcher.Invoke(OnTextModeToggle);
        _tray.PauseToggleRequested += () => Dispatcher.Invoke(TogglePause);
        _tray.QuitRequested += () => Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
        _tray.SettingsRequested += () => Dispatcher.Invoke(OpenSettings);
        _tray.HelpRequested += () => Dispatcher.Invoke(OpenShortcutHelp);
        _commandHotkeys = new GlobalCommandHotkeys(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        _commandHotkeys.HelpRequested += OpenShortcutHelp;
        _commandHotkeys.QuitRequested += () => System.Windows.Application.Current.Shutdown();
        if (!_commandHotkeys.HelpAvailable || !_commandHotkeys.QuitAvailable)
            _ = Dispatcher.BeginInvoke(OpenShortcutHelp);
        _tray.AccountRequested += () => Dispatcher.Invoke(() =>
        {
            OnDismiss();
            if (App.AccountSession is null)
                MessageBox.Show("This is a local developer build. Paid accounts require a configured paid-release build.", "Whisper");
            else new DesktopSignInWindow(App.AccountSession).ShowDialog();
        });

        GetCursorPos(out var pos);
        MoveToMonitor(DisplayTopology.At(pos.X, pos.Y));
        var cursor = PointFromScreen(new Point(pos.X, pos.Y));
        _blobX = cursor.X;
        _blobY = cursor.Y + 50;
        _targetX = _blobX;
        _targetY = _blobY;

        await _brainClient.ConnectAsync();
    }

    private void MakeOverlayClickThrough()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | 0x08000000);
        DisplayTopology.ExcludeFromCapture(hwnd);

    }

    private void CursorTrackerTick(object? sender, EventArgs e)
    {
        GetCursorPos(out var pos);
        try
        {
            var monitor = DisplayTopology.At(pos.X, pos.Y);
            if (monitor != _cursorMonitor)
            {
                MoveToMonitor(monitor);
                var current = PointFromScreen(new Point(pos.X, pos.Y));
                _blobX = current.X; _blobY = current.Y + 50;
            }
        }
        catch (System.ComponentModel.Win32Exception) { return; }
        var cursor = PointFromScreen(new Point(pos.X, pos.Y));
        _targetX = cursor.X;
        _targetY = cursor.Y + 50;
    }

    private void AnimationTick(object? sender, EventArgs e)
    {
        double springForce = 0.15;
        double dx = _targetX - _blobX;
        double dy = _targetY - _blobY;
        _blobX += dx * (_settings.ReducedMotion ? 1 : springForce);
        _blobY += dy * (_settings.ReducedMotion ? 1 : springForce);

        Canvas.SetLeft(BlobContainer, _blobX - 20);
        Canvas.SetTop(BlobContainer, _blobY - 20);

        _breathPhase = _settings.ReducedMotion ? 0 : _breathPhase + 0.05;

        switch (_stateMachine.Current)
        {
            case WhisperState.Idle: AnimateIdle(); break;
            case WhisperState.Listening: AnimateListening(); break;
            case WhisperState.Transcribing: AnimateThinking(); break;
            case WhisperState.Thinking: AnimateThinking(); break;
            case WhisperState.Responding: AnimateResponding(); break;
        }

        if (ResponseBubble.Visibility == Visibility.Visible)
        {
            var area = _cursorMonitor?.WorkArea;
            var topLeft = area is null ? new Point(0, 0) : PointFromScreen(new Point(area.Left, area.Top));
            var bottomRight = area is null ? new Point(Math.Max(1, OverlayCanvas.ActualWidth), Math.Max(1, OverlayCanvas.ActualHeight))
                : PointFromScreen(new Point((double)area.Left + area.Width, (double)area.Top + area.Height));
            var position = WindowPlacement.Clamp(_blobX + 30, _blobY - 20,
                ResponseBubble.ActualWidth, ResponseBubble.ActualHeight,
                topLeft.X, topLeft.Y, Math.Max(1, bottomRight.X - topLeft.X), Math.Max(1, bottomRight.Y - topLeft.Y));
            Canvas.SetLeft(ResponseBubble, position.X);
            Canvas.SetTop(ResponseBubble, position.Y);
        }
        Canvas.SetLeft(StatusText, _blobX - 20);
        Canvas.SetTop(StatusText, _blobY + 28);
    }

    private void AnimateIdle()
    {
        double breathScale = 1.0 + Math.Sin(_breathPhase) * 0.05;
        BlobBody.Width = 40 * breathScale;
        BlobBody.Height = 40 * breathScale;
        BlobEye.Opacity = 0.5 + Math.Sin(_breathPhase * 0.5) * 0.2;
        BlobGlow.Opacity = 0;
    }

    private void AnimateListening()
    {
        double pulse = 1.0 + Math.Sin(_breathPhase * 3) * 0.08;
        BlobBody.Width = 48 * pulse;
        BlobBody.Height = 48 * pulse;
        BlobGlow.Opacity = 0.4 + Math.Sin(_breathPhase * 2) * 0.3;
        BlobGlow.Width = 70 * pulse;
        BlobGlow.Height = 70 * pulse;
        Canvas.SetLeft(BlobGlow, (40 - BlobGlow.Width) / 2);
        Canvas.SetTop(BlobGlow, (40 - BlobGlow.Height) / 2);
        BlobEye.Fill = new SolidColorBrush(Color.FromRgb(64, 169, 255));
        BlobEye.Opacity = 1.0;
        BlobBody.Fill = new RadialGradientBrush(
            Color.FromArgb(255, 40, 60, 100),
            Color.FromArgb(255, 20, 30, 60));
    }

    private void AnimateThinking()
    {
        double spin = _breathPhase * 4;
        double pulse = 1.0 + Math.Sin(spin) * 0.03;
        BlobBody.Width = 44 * pulse;
        BlobBody.Height = 44 * pulse;
        double eyeRadius = 8;
        Canvas.SetLeft(BlobEye, 16 + Math.Cos(spin) * eyeRadius);
        Canvas.SetTop(BlobEye, 16 + Math.Sin(spin) * eyeRadius);
        BlobEye.Fill = new SolidColorBrush(Color.FromRgb(255, 200, 60));
        BlobGlow.Opacity = 0.2 + Math.Sin(spin * 0.5) * 0.15;
    }

    private void AnimateResponding()
    {
        double talk = Math.Abs(Math.Sin(_breathPhase * 5));
        BlobBody.Width = 42 + talk * 4;
        BlobBody.Height = 42 - talk * 2;
        BlobEye.Fill = new SolidColorBrush(Color.FromRgb(100, 255, 150));
        BlobEye.Opacity = 1.0;
        BlobGlow.Opacity = 0.15;
    }

    private void SetState(WhisperState newState)
    {
        if (_stateMachine.Current == newState)
            return;

        if (!_stateMachine.TryTransition(newState))
            throw new InvalidOperationException(
                $"Invalid Whisper state transition: {_stateMachine.Current} -> {newState}");
        BlobBody.Fill = new RadialGradientBrush(
            Color.FromArgb(255, 58, 58, 74),
            Color.FromArgb(255, 30, 30, 46));
        BlobEye.Fill = new SolidColorBrush(Color.FromArgb(255, 110, 110, 138));
        Canvas.SetLeft(BlobEye, 16);
        Canvas.SetTop(BlobEye, 16);

        Dispatcher.Invoke(() =>
        {
            switch (newState)
            {
                case WhisperState.Listening:
                    StatusText.Text = "Listening...";
                    StatusText.Visibility = Visibility.Visible;
                    break;
                case WhisperState.Transcribing:
                    StatusText.Text = "Transcribing...";
                    StatusText.Visibility = Visibility.Visible;
                    break;
                case WhisperState.Thinking:
                    StatusText.Text = "Thinking...";
                    StatusText.Visibility = Visibility.Visible;
                    break;
                default:
                    StatusText.Visibility = Visibility.Collapsed;
                    break;
            }
        });
    }

    private void OnVoiceModeStart()
    {
        Dispatcher.Invoke(async () =>
        {
            if (_settingsWindow is not null) return;
            _voiceKeyHeld = true;
            ClearWalkthrough();
            if (!_interactions.TryBeginTurn(out var accessCancellation)) return;
            try
            {
                await RequireAccountAsync(accessCancellation);
                accessCancellation.ThrowIfCancellationRequested();
                if (!_voiceKeyHeld) { _interactions.CancelTurn(); return; }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception) { _interactions.CancelTurn(); OnBrainError("Account access could not be verified. Sign in again."); return; }
            _voiceScreenForTurn = _settings.IncludeScreenForVoice;
            _screenSharedForTurn = _voiceScreenForTurn;
            GetCursorPos(out var voiceCursor);
            _voiceMonitor = DisplayTopology.At(voiceCursor.X, voiceCursor.Y);
            _voiceWindowHandle = AppDetectionService.GetActiveWindowHandle();
            _captureForTurn = null;
            StopDismissTimer();
            _speechSynthesis.Cancel();
            _ = _brainClient.CancelActiveRequestAsync();
            _currentResponse = "";
            SetState(WhisperState.Listening);
            _queryWindow?.Close();
            try { _audioRecorder.CancelRecording(); _audioRecorder.StartRecording(); }
            catch (Exception)
            {
                _interactions.CancelTurn();
                OnBrainError("Cannot start the microphone. Check its connection and Windows microphone permissions.");
            }
            ClearHighlights();
        });
    }

    private async void OnVoiceModeStop()
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            _voiceKeyHeld = false;
            if (_interactions.IsPaused || _stateMachine.Current != WhisperState.Listening)
                return;
            var token = _interactions.CurrentToken;
            SetState(WhisperState.Transcribing);
            GetCursorPos(out var pos);
            try
            {
                string? audioBase64 = await _audioRecorder.StopRecordingAsync(token);
                token.ThrowIfCancellationRequested();
                if (audioBase64 is null) { OnBrainError("No audio was recorded. Hold the voice shortcut while speaking."); return; }
                await RequireAccountAsync(token);
                token.ThrowIfCancellationRequested();
                var request = WhisperRequestFactory.CreateWithCapture(null, audioBase64, _voiceScreenForTurn,
                    () => CaptureSelectedMonitor(_voiceMonitor!), AppDetectionService.GetActiveWindowInfo);
                request.AccountToken = App.AccountSession?.AccessToken;
                _turnWindowHandle = _voiceWindowHandle;
                _turnMonitor = _voiceMonitor;
                _turnScreenText = null;
                if (_voiceScreenForTurn)
                {
                    _turnScreenText = await ScreenTextContextService.ReadSnapshotAsync(_voiceWindowHandle, _voiceMonitor!.Bounds, token);
                    request.ScreenText = _turnScreenText?.Text;
                    request.ScreenTargets = _turnScreenText?.Targets;
                }
                token.ThrowIfCancellationRequested();
                await _brainClient.SendQueryAsync(request, token);
            }
            catch (OperationCanceledException) { /* Dismiss/pause owns the UI state. */ }
            catch (Exception) { if (!token.IsCancellationRequested) OnBrainError("Could not capture or send the voice question. Try again."); }
        });
    }

    private void OnTextModeToggle()
    {
        Dispatcher.Invoke(() =>
        {
            if (_interactions.IsPaused || _settingsWindow is not null) return;
            if (_queryWindow is not null)
            {
                _queryWindow.Close();
            }
            else
            {
                if (_stateMachine.Current == WhisperState.Listening)
                    return;
                StopDismissTimer();
                ClearWalkthrough();
                _interactions.CancelTurn();
                _speechSynthesis.Cancel();
                _ = _brainClient.CancelActiveRequestAsync();
                _queryActiveWindow = AppDetectionService.GetActiveWindowInfo();
                _queryWindowHandle = AppDetectionService.GetActiveWindowHandle();
                SetState(WhisperState.TextInput);
                ResponseBubble.Visibility = Visibility.Collapsed;
                ClearHighlights();
                GetCursorPos(out var pos);
                _queryMonitor = DisplayTopology.At(pos.X, pos.Y);
                var window = new QueryInputWindow(pos.X, pos.Y) { Owner = this };
                _queryWindow = window;
                window.Submitted += SubmitTextQuery;
                window.Closed += (_, _) =>
                {
                    if (ReferenceEquals(_queryWindow, window))
                        _queryWindow = null;
                    if (_stateMachine.Current == WhisperState.TextInput)
                        SetState(WhisperState.Idle);
                };
                window.Show();
            }
        });
    }

    private async void SubmitTextQuery(string userText, bool includeScreen)
    {
        _guideClickObserver?.Dispose(); _guideClickObserver = null;
        if (!_interactions.TryBeginTurn(out var token)) return;
        var continuing = _continuingWalkthrough;
        _continuingWalkthrough = false;
        try
        {
            await RequireAccountAsync(token);
            token.ThrowIfCancellationRequested();
            _speechSynthesis.Cancel();
            _currentResponse = "";
            SetState(WhisperState.Thinking);
            _screenSharedForTurn = includeScreen;
            _captureForTurn = null;
            _queryWindow?.Close();

            GetCursorPos(out var pos);
            token.ThrowIfCancellationRequested();
            var request = WhisperRequestFactory.CreateWithCapture(userText, null, includeScreen,
                () => CaptureSelectedMonitor(_queryMonitor!), () => _queryActiveWindow);
            request.AccountToken = App.AccountSession?.AccessToken;
            _turnWindowHandle = _queryWindowHandle;
            _turnMonitor = _queryMonitor;
            _turnScreenText = null;
            if (includeScreen)
            {
                _turnScreenText = await ScreenTextContextService.ReadSnapshotAsync(_queryWindowHandle, _queryMonitor!.Bounds, token);
                request.ScreenText = _turnScreenText?.Text;
                request.ScreenTargets = _turnScreenText?.Targets;
                request.Guide = continuing || Guidance.Requested(userText);
                if (request.Guide && !continuing)
                { _walkthroughGoal = userText; _walkthroughCompleted.Clear(); _walkthroughClickedLabels.Clear(); _walkthroughFailedLabels.Clear(); }
                request.GuideGoal = _walkthroughGoal is null ? null : _walkthroughGoal[..Math.Min(1000, _walkthroughGoal.Length)];
                request.GuideClicked = _walkthroughClickedLabels.TakeLast(24).ToArray();
                request.GuideFailed = _walkthroughFailedLabels.TakeLast(24).ToArray();
            }
            token.ThrowIfCancellationRequested();
            await _brainClient.SendQueryAsync(request, token);
        }
        catch (OperationCanceledException) { /* Dismiss/pause owns the UI state. */ }
        catch (Exception)
        {
            OnBrainError("Could not capture or send the question. Try again.");
        }
    }

    private void OnDismiss()
    {
        ClearWalkthrough();
        _interactions.CancelTurn();
        _speechSynthesis.Cancel();
        _ = _brainClient.CancelActiveRequestAsync();
        Dispatcher.Invoke(() =>
        {
            ResponseBubble.Visibility = Visibility.Collapsed;
            _audioRecorder.CancelRecording();
            ClearHighlights();
            MakeOverlayClickThrough();
            SetState(WhisperState.Idle);
            _queryWindow?.Close();
            _currentResponse = "";
        });
    }

    private void OnBrainTextChunk(string chunk)
    {
        Dispatcher.Invoke(() =>
        {
            if (_stateMachine.Current is not (WhisperState.Thinking or WhisperState.Responding))
                return;
            SetState(WhisperState.Responding);
            _currentResponse += chunk;
            ResponseText.Text = _currentResponse;
            ResponseBubble.Visibility = Visibility.Visible;
        });
    }

    private void OnBrainFullResponse(WhisperResponse response)
    {
        Dispatcher.Invoke(() =>
        {
            if (_stateMachine.Current is not (WhisperState.Thinking or WhisperState.Responding))
                return;
            SetState(WhisperState.Responding);
            _currentResponse = response.Text;
            ResponseText.Text = _currentResponse;
            ResponseBubble.Visibility = Visibility.Visible;
            if (response.Guide is not null && _screenSharedForTurn && _captureForTurn is not null
                && response.Capture?.CaptureId == _captureForTurn.CaptureId)
            {
                StopDismissTimer();
                _ = ShowWalkthroughAsync(response.Guide, _interactions.CurrentToken);
                return;
            }

            if (_screenSharedForTurn && _captureForTurn is not null && response.Capture?.CaptureId == _captureForTurn.CaptureId && response.Highlights != null)
            {
                _annotations?.Close();
                _annotations = new AnnotationWindow(_captureForTurn, response.Highlights);
                _annotations.Show();
            }

            if (!_settings.SpeakResponses || !_speechSynthesis.Speak(response.Text))
                ScheduleDismiss();
        });
    }

    private void OnSpeechCompleted()
    {
        Dispatcher.Invoke(ScheduleDismiss);
    }

    private void OnBrainError(string error)
    {
        Dispatcher.Invoke(() =>
        {
            if (_interactions.IsPaused) return;
            _currentResponse = "Error: " + error;
            ResponseText.Text = _currentResponse;
            ResponseBubble.Visibility = Visibility.Visible;
            SetState(WhisperState.Idle);
        });
    }

    private void DrawHighlight(ScreenHighlight highlight)
    {
        if (highlight.Type is not ("box" or "pulse")) return;
        // The current capture is primary-display physical pixels, not WPF DIPs.
        var origin = PointFromScreen(new Point(highlight.X, highlight.Y));
        var corner = PointFromScreen(new Point((double)highlight.X + highlight.Width, (double)highlight.Y + highlight.Height));
        var x = origin.X;
        var y = origin.Y;
        var width = corner.X - x;
        var height = corner.Y - y;
        if (highlight.Type == "box")
        {
            var clipped = HighlightGeometry.ClipBox(x, y, width, height, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight);
            if (clipped is null) return;
            (x, y, width, height) = clipped.Value;
        }
        else if (x < 0 || y < 0 || x >= OverlayCanvas.ActualWidth || y >= OverlayCanvas.ActualHeight) return;
        switch (highlight.Type)
        {
            case "box":
                var rect = new Rectangle
                {
                    Width = width,
                    Height = height,
                    Stroke = new SolidColorBrush(Color.FromArgb(200, 64, 169, 255)),
                    StrokeThickness = 2.5,
                    RadiusX = 4, RadiusY = 4,
                    Fill = new SolidColorBrush(Color.FromArgb(30, 64, 169, 255))
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                OverlayCanvas.Children.Add(rect);
                _highlightElements.Add(rect);
                break;
            case "pulse":
                var pulse = new Ellipse
                {
                    Width = 30, Height = 30,
                    Fill = new SolidColorBrush(Color.FromArgb(150, 255, 100, 100))
                };
                Canvas.SetLeft(pulse, x - 15);
                Canvas.SetTop(pulse, y - 15);
                OverlayCanvas.Children.Add(pulse);
                _highlightElements.Add(pulse);
                var scaleAnim = new DoubleAnimation(1.0, 2.0, TimeSpan.FromSeconds(0.8))
                    { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
                var transform = new ScaleTransform(1, 1, 15, 15);
                pulse.RenderTransform = transform;
                if (!_settings.ReducedMotion)
                {
                    transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
                break;
        }

        if (!string.IsNullOrEmpty(highlight.Label))
        {
            var label = new TextBlock
            {
                Text = highlight.Label[..Math.Min(200, highlight.Label.Length)],
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 40)),
                Padding = new Thickness(6, 3, 6, 3),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11
            };
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, Math.Max(0, y - 22));
            OverlayCanvas.Children.Add(label);
            _highlightElements.Add(label);
        }
    }

    private void ClearHighlights()
    {
        _annotations?.Close();
        _annotations = null;
        foreach (var el in _highlightElements)
            OverlayCanvas.Children.Remove(el);
        _highlightElements.Clear();
    }

    private void OnBrainTranscript(string transcript)
    {
        Dispatcher.Invoke(() =>
        {
            if (_stateMachine.Current != WhisperState.Transcribing)
                return;
            SetState(WhisperState.Thinking);
            StatusText.Text = $"Heard: {transcript}";
            if (_screenSharedForTurn && Guidance.Requested(transcript))
            { _walkthroughGoal = transcript; _walkthroughCompleted.Clear(); }
            StatusText.Visibility = Visibility.Visible;
        });
    }

    private void ClearWalkthrough()
    {
        Dispatcher.Invoke(() =>
        {
            _walkthrough?.Close(); _walkthrough = null;
            _guideClickObserver?.Dispose(); _guideClickObserver = null;
            _guideAdvancing = false;
            _walkthroughGoal = null; _walkthroughInstruction = null;
            _walkthroughCompleted.Clear(); _continuingWalkthrough = false;
            _walkthroughClickedLabels.Clear();
            _walkthroughFailedLabels.Clear(); _walkthroughTargetLabel = null;
            _observedGuideClickPending = false;
        });
    }

    private async Task ShowWalkthroughAsync(GuideStep step, CancellationToken cancellation)
    {
        var monitor = _turnMonitor; var capture = _captureForTurn;
        var snapshot = _turnScreenText; var window = _turnWindowHandle;
        if (monitor is null || capture is null) return;
        try
        {
            var original = snapshot?.Targets.FirstOrDefault(target => target.Id == step.TargetId);
            if (original is not null && !Guidance.CanOffer(original.Label, _walkthroughClickedLabels, _walkthroughFailedLabels))
            {
                original = null;
                step = new GuideStep { Instruction = "I will not repeat a clicked or failed step. Open the relevant app or menu manually and use Refresh, or stop and describe what went wrong." };
            }
            ScreenTarget? target = null;
            if (original is not null && AppDetectionService.GetActiveWindowHandle() == window)
            {
                var fresh = await ScreenTextContextService.ReadSnapshotAsync(window, monitor.Bounds, cancellation);
                cancellation.ThrowIfCancellationRequested();
                if (fresh is not null && AppDetectionService.GetActiveWindowHandle() == window)
                {
                    target = Guidance.Resolve(original, fresh.Targets, monitor.Bounds);
                    if (target is not null) _turnScreenText = fresh; // Observe changes from the live pre-click baseline.
                }
            }
            cancellation.ThrowIfCancellationRequested();
            _walkthrough?.Close();
            _guideClickObserver?.Dispose(); _guideClickObserver = null;
            _guideAdvancing = false;
            ClearHighlights();
            if ((GetAsyncKeyState(1) & 0x8000) != 0 || (GetAsyncKeyState(2) & 0x8000) != 0
                || (GetAsyncKeyState(4) & 0x8000) != 0) target = null; // Never interrupt a held mouse button/drag.
            var anchorX = monitor.WorkArea.Left + monitor.WorkArea.Width * 3 / 4;
            var anchorY = monitor.WorkArea.Top + 50;
            if (target is not null)
            {
                var centerX = target.X + target.Width / 2; var centerY = target.Y + target.Height / 2;
                if (centerX > monitor.Bounds.Left + monitor.Bounds.Width / 2)
                    anchorX = monitor.WorkArea.Left + monitor.WorkArea.Width / 4;
                if (centerY < monitor.Bounds.Top + monitor.Bounds.Height / 2)
                    anchorY = monitor.WorkArea.Top + Math.Max(30, monitor.WorkArea.Height - 300);
                _annotations = new AnnotationWindow(capture with { PreviewWidth = 0, PreviewHeight = 0 },
                    new[] { new ScreenHighlight { X = target.X - capture.OriginX, Y = target.Y - capture.OriginY,
                        Width = target.Width, Height = target.Height, Type = "box", Label = target.Label } });
                _annotations.Show();
                if (AppDetectionService.GetActiveWindowHandle() != window || !SetCursorPos(centerX, centerY))
                { target = null; ClearHighlights(); }
            }
            var instruction = step.Instruction;
            if (step.TargetId is not null && target is null)
                instruction += "\nI could not re-check that control safely. Click the app and use Refresh.";
            _walkthroughInstruction = step.Instruction;
            _walkthroughTargetLabel = original?.Label;
            var card = new WalkthroughWindow(anchorX, anchorY);
            _walkthrough = card;
            card.SetStep(instruction, !step.Done && target is null);
            card.EnableFailureFeedback(!step.Done && original is not null);
            card.NextRequested += ContinueWalkthrough;
            card.FailedRequested += () =>
            {
                if (_walkthroughTargetLabel is null) return;
                if (_walkthroughFailedLabels.Count >= 24)
                { OnDismiss(); return; } // Bounded session: start a fresh question instead of forgetting failures.
                _walkthroughFailedLabels.Add(_walkthroughTargetLabel);
                _observedGuideClickPending = false;
                ContinueWalkthrough();
            };
            card.StopRequested += OnDismiss;
            ResponseBubble.Visibility = Visibility.Collapsed;
            card.Show();
            if (target is not null && !step.Done)
            {
                try
                {
                    var observedTarget = target;
                    _guideClickObserver = new GuideClickObserver(observedTarget, window, () =>
                        Dispatcher.BeginInvoke(new Action(() => _ = AdvanceAfterGuideClickAsync(cancellation, observedTarget.Label))));
                }
                catch (System.ComponentModel.Win32Exception)
                { card.SetStep(instruction + "\nClick observation is unavailable; use Refresh after clicking.", true); }
            }
            if (_settings.SpeakResponses) _speechSynthesis.Speak(instruction);
        }
        catch (OperationCanceledException) { }
        catch (Exception) { if (!cancellation.IsCancellationRequested) OnBrainError("Could not show this guide step safely. Try the question again."); }
    }

    private static string GuideContextSignature(ScreenTextSnapshot? snapshot)
        => snapshot is null ? "" : snapshot.Text + "|" + string.Join("|", snapshot.Targets.Select(target => target.RuntimeId + ":" + target.Label + ":" + target.State));

    private async Task AdvanceAfterGuideClickAsync(CancellationToken cancellation, string clickedLabel)
    {
        if (_guideAdvancing || cancellation.IsCancellationRequested || _turnMonitor is null) return;
        _guideAdvancing = true;
        _observedGuideClickPending = true;
        _walkthroughClickedLabels.Add(clickedLabel);
        if (_walkthroughClickedLabels.Count > 24) _walkthroughClickedLabels.RemoveAt(0);
        _guideClickObserver?.Dispose(); _guideClickObserver = null;
        _walkthrough?.SetStep("Waiting for the app to update…", false);
        var before = GuideContextSignature(_turnScreenText);
        var monitor = _turnMonitor;
        try
        {
            await Task.Delay(200, cancellation);
            string? previous = null; IntPtr previousWindow = IntPtr.Zero;
            for (var attempt = 0; attempt < 8; attempt++)
            {
                cancellation.ThrowIfCancellationRequested();
                var window = AppDetectionService.GetActiveWindowHandle();
                var snapshot = await ScreenTextContextService.ReadSnapshotAsync(window, monitor.Bounds, cancellation);
                var signature = GuideContextSignature(snapshot);
                if (snapshot is not null && signature != before && signature == previous && window == previousWindow)
                {
                    cancellation.ThrowIfCancellationRequested();
                    ContinueWalkthrough();
                    return;
                }
                previous = signature; previousWindow = window;
                await Task.Delay(150, cancellation);
            }
            _walkthrough?.SetStep("I saw your click, but cannot yet verify a stable app change. Let it load, then use Refresh.", true);
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        { if (!cancellation.IsCancellationRequested) _walkthrough?.SetStep("I could not read the updated app safely. Use Refresh when it is ready.", true); }
    }

    private void ContinueWalkthrough()
    {
        if (_interactions.IsPaused || _walkthroughGoal is null || _turnMonitor is null) return;
        if (_observedGuideClickPending && _walkthroughInstruction is not null)
            _walkthroughCompleted.Add(_walkthroughInstruction);
        _observedGuideClickPending = false;
        _queryWindowHandle = AppDetectionService.GetActiveWindowHandle();
        _queryActiveWindow = AppDetectionService.GetActiveWindowInfo();
        _queryMonitor = _turnMonitor;
        _guideClickObserver?.Dispose(); _guideClickObserver = null;
        _speechSynthesis.Cancel();
        ClearHighlights();
        _walkthrough?.SetStep("Checking the current app for the next step…", false);
        _continuingWalkthrough = true;
        SubmitTextQuery($"Continue this walkthrough: {_walkthroughGoal}\nThe user clicked these suggested steps (verify success from current controls; do not repeat failed steps): "
            + string.Join("; ", _walkthroughCompleted.TakeLast(6)) + "\nGive only the next visible step.", true);
    }

    private void ScheduleDismiss()
    {
        if (_walkthrough is not null) return;
        if (_interactions.IsPaused) return;
        StopDismissTimer();
        _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _dismissTimer.Tick += (s, ev) =>
        {
            StopDismissTimer();
            if (_brainClient.ActiveRequestId is null)
                OnDismiss();
        };
        _dismissTimer.Start();
    }

    private void StopDismissTimer()
    {
        _dismissTimer?.Stop();
        _dismissTimer = null;
    }

    private void TogglePause()
    {
        if (!_interactions.IsPaused)
        {
            _interactions.Pause();
            _hotkeyManager.Stop();
            OnDismiss();
            _animationTimer.Stop();
            _cursorTracker.Stop();
            Hide();
            _tray?.SetPaused(true);
        }
        else
        {
            try { _hotkeyManager.Start(); }
            catch (Exception)
            {
                MessageBox.Show("Could not enable hotkeys. Whisper remains paused.", "Whisper");
                return;
            }
            _interactions.Resume();
            Show();
            MakeOverlayClickThrough();
            _animationTimer.Start();
            _cursorTracker.Start();
            _tray?.SetPaused(false);
        }
    }

    private void OpenShortcutHelp()
    {
        if (_helpWindow is not null) { _helpWindow.Activate(); return; }
        OnDismiss();
        var window = new ShortcutHelpWindow(_commandHotkeys?.HelpAvailable == true,
                                            _commandHotkeys?.QuitAvailable == true) { Owner = this };
        _helpWindow = window;
        window.Closed += (_, _) => _helpWindow = null;
        window.Show();
        GetCursorPos(out var cursor);
        try { MonitorWindowPlacement.Place(new System.Windows.Interop.WindowInteropHelper(window).Handle, cursor.X, cursor.Y); }
        catch (System.ComponentModel.Win32Exception) { /* Center-screen fallback. */ }
    }

    private void OpenSettings()
    {
        if (_settingsWindow is not null) { _settingsWindow.Activate(); return; }
        OnDismiss();
        var window = new SettingsWindow(_settingsStore, _settings, _settingsWarning, () => _brainClient.IsConnected) { Owner = this };
        _settingsWindow = window;
        window.Saved += settings =>
        {
            _settings = settings;
            _settingsWarning = null;
            if (!settings.SpeakResponses) _speechSynthesis.Cancel();
        };
        window.Closed += (_, _) => _settingsWindow = null;
        window.Show();
    }

    protected override void OnClosed(EventArgs e)
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _annotations?.Close();
        _interactions.Dispose();
        _tray?.Dispose();
        _queryWindow?.Close();
        _settingsWindow?.Close();
        _helpWindow?.Close();
        _commandHotkeys?.Dispose();
        _hotkeyManager.Dispose();
        _audioRecorder.Dispose();
        _speechSynthesis.Dispose();
        _brainClient.Dispose();
        _animationTimer.Stop();
        _cursorTracker.Stop();
        StopDismissTimer();
        base.OnClosed(e);
    }

    private void MoveToMonitor(DesktopMonitor monitor)
    {
        DisplayTopology.FitWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle, monitor.Bounds);
        _cursorMonitor = monitor;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        if (_interactions.IsPaused) { ClearHighlights(); return; }
        OnDismiss(); // Old capture geometry becomes invalid after topology/rotation/scale changes.
        _captureForTurn = null;
        _cursorMonitor = null;
    });

    private ScreenSnapshot CaptureSelectedMonitor(DesktopMonitor monitor)
    {
        if (monitor is null) throw new InvalidOperationException("Select a monitor before capturing.");
        var current = DisplayTopology.At(monitor.Bounds.Left + monitor.Bounds.Width / 2,
            monitor.Bounds.Top + monitor.Bounds.Height / 2);
        if (current != monitor) throw new InvalidOperationException("Display layout changed. Ask again.");
        var visible = IsVisible;
        // Hide our surface and flush composition as a fallback even if display affinity is unavailable.
        Hide();
        try
        {
            DisplayTopology.FlushComposition();
            var snapshot = _screenCapture.CaptureMonitor(monitor);
            _captureForTurn = snapshot.Descriptor;
            return snapshot;
        }
        finally { if (visible && !_interactions.IsPaused) { Show(); MakeOverlayClickThrough(); } }
    }
}
