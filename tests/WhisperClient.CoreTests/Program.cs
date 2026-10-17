using WhisperClient.Core;

if (args.Contains("--token-path-smoke"))
{
    var previous = Environment.GetEnvironmentVariable("WHISPER_AUTH_TOKEN_FILE");
    try
    {
        Environment.SetEnvironmentVariable("WHISPER_AUTH_TOKEN_FILE", "configured-token-path");
        if (new LocalAuthTokenProvider().TokenPath != "configured-token-path")
            throw new InvalidOperationException("Configured token path was ignored.");
        if (new LocalAuthTokenProvider("explicit-token-path").TokenPath != "explicit-token-path")
            throw new InvalidOperationException("Explicit token path did not take precedence.");
        Environment.SetEnvironmentVariable("WHISPER_AUTH_TOKEN_FILE", " ");
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Whisper", "orchestrator.token");
        if (new LocalAuthTokenProvider().TokenPath != expected)
            throw new InvalidOperationException("Default token path changed.");
        Console.WriteLine("Configured, explicit and default token-path checks passed. No secrets read or printed.");
    }
    finally { Environment.SetEnvironmentVariable("WHISPER_AUTH_TOKEN_FILE", previous); }
    return;
}

if (args.Contains("--shortcut-help-smoke"))
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            foreach (var available in new[] { true, false })
            {
                var card = new WhisperClient.UI.ShortcutHelpWindow(available, available);
                card.Close();
            }
            using var source = new System.Windows.Interop.HwndSource(
                new System.Windows.Interop.HwndSourceParameters("Whisper shortcut fixture") { Width = 1, Height = 1 });
            using var commands = new GlobalCommandHotkeys(source.Handle);
            Console.WriteLine($"Global help registration: {commands.HelpAvailable}; quit registration: {commands.QuitAvailable}. Conflicts use tray fallback.");
            commands.Dispose(); // disposal is idempotent; no desktop input sent
        }
        catch (Exception error) { failure = error; }
    });
    thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
    if (failure is not null) throw failure;
    Console.WriteLine("Help card and hotkey lifecycle smoke passed; no keyboard input sent.");
    return;
}

// Opt-in integration fixture: never reads another user application.
if (args.Contains("--guide-card-smoke"))
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            var card = new WhisperClient.UI.WalkthroughWindow(0, 0);
            card.SetStep("Click Settings, then press Next step.");
            card.Close();
        }
        catch (Exception error) { failure = error; }
    });
    thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
    if (failure is not null) throw failure;
    Console.WriteLine("Walkthrough card construction/styles passed; no window or pointer movement shown.");
    return;
}
if (args.Contains("--screen-text-fixture"))
{
    var thread = new Thread(() =>
    {
        var panel = new System.Windows.Controls.StackPanel();
        panel.Children.Add(new System.Windows.Controls.TextBlock { Text =
            "Download status: vision model installed successfully. No download running. Ready for local testing.",
            TextWrapping = System.Windows.TextWrapping.Wrap });
        panel.Children.Add(new System.Windows.Controls.TextBox { Text = "PRIVATE_EDIT_VALUE" });
        panel.Children.Add(new System.Windows.Controls.Button { Content = "Settings", Height = 30 });
        panel.Children.Add(new System.Windows.Controls.PasswordBox { Password = "PRIVATE_PASSWORD_VALUE" });
        var window = new System.Windows.Window { Title = "Whisper screen text fixture", Width = 500, Height = 260,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen, Content = panel };
        new System.Windows.Application().Run(window);
    });
    thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
    return;
}

if (args.Contains("--screen-text-smoke"))
{
    var fixture = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = Environment.ProcessPath!, UseShellExecute = false,
        ArgumentList = { System.Reflection.Assembly.GetExecutingAssembly().Location, "--screen-text-fixture" }
    })!;
    try
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (fixture.MainWindowHandle == IntPtr.Zero && clock.ElapsedMilliseconds < 5000)
        { Thread.Sleep(50); fixture.Refresh(); }
        var start = System.Diagnostics.Stopwatch.StartNew();
        var labels = await WhisperClient.Services.ScreenTextContextService.ReadAsync(fixture.MainWindowHandle,
            new PixelBounds(-100000, -100000, 200000, 200000), CancellationToken.None);
        if (labels is null || !labels.Contains("vision model installed") || labels.Contains("PRIVATE_"))
            throw new InvalidOperationException("Visible text/password exclusion fixture failed.");
        Console.WriteLine($"Screen text fixture passed in {start.ElapsedMilliseconds} ms; editable/password values excluded.");
        var snapshot = await WhisperClient.Services.ScreenTextContextService.ReadSnapshotAsync(fixture.MainWindowHandle,
            new PixelBounds(-100000, -100000, 200000, 200000), CancellationToken.None);
        var target = snapshot?.Targets.SingleOrDefault(item => item.Label == "Settings");
        if (target is null || string.IsNullOrEmpty(target.RuntimeId) || target.Width <= 0 || target.Height <= 0)
            throw new InvalidOperationException("Settings control grounding fixture failed.");
        Console.WriteLine("Live Settings button identity and physical bounds verified; no pointer or click sent by this test.");
    }
    finally { fixture.CloseMainWindow(); if (!fixture.WaitForExit(2000)) fixture.Kill(); fixture.Dispose(); }
    return;
}

var assertions = 0;
void Assert(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException($"Failed: {name}");
    assertions++;
}

Assert(CapturePreviewGeometry.Size(1920, 1080) == (640, 360), "full HD preview bounded");
var grounded = new ScreenTarget { Id = "t0", Label = "Settings", RuntimeId = "live.1", X = -1800, Y = 10, Width = 80, Height = 30 };
var clickGate = new GuideClickGate(new PixelBounds(-1800, 10, 80, 30));
Assert(!clickGate.Observe(0x0201, -1780, 20, true, false, 100), "guide click down only arms");
Assert(clickGate.Observe(0x0202, -1780, 20, true, false, 200), "real highlighted target click advances");
Assert(!clickGate.Observe(0x0202, -1780, 20, true, false, 210), "guide click is consumed once");
var wrongWindowGate = new GuideClickGate(new PixelBounds(0, 0, 100, 40));
wrongWindowGate.Observe(0x0201, 10, 10, false, false, 100);
Assert(!wrongWindowGate.Observe(0x0202, 10, 10, true, false, 200), "unrelated foreground click ignored");
var injectedGate = new GuideClickGate(new PixelBounds(0, 0, 100, 40));
injectedGate.Observe(0x0201, 10, 10, true, true, 100);
Assert(!injectedGate.Observe(0x0202, 10, 10, true, true, 200), "injected clicks ignored");
var dragGate = new GuideClickGate(new PixelBounds(0, 0, 100, 40));
dragGate.Observe(0x0201, 10, 10, true, false, 100);
dragGate.Observe(0x0200, 200, 10, true, false, 150);
Assert(!dragGate.Observe(0x0202, 10, 10, true, false, 200), "drag out and back never advances");
var guideBounds = new PixelBounds(-1920, 0, 1920, 1080);
Assert(Guidance.Resolve(grounded, new[] { grounded }, guideBounds) == grounded, "known negative-monitor control resolved");
Assert(Guidance.Resolve(grounded, new[] { grounded with { RuntimeId = "changed" } }, guideBounds) is null, "stale runtime identity rejected");
Assert(Guidance.Resolve(grounded, new[] { grounded with { Label = "Delete" } }, guideBounds) is null, "changed label rejected");
Assert(Guidance.Resolve(grounded, new[] { grounded with { State = "on" } }, guideBounds) is null, "changed toggle state rejected");
Assert(Guidance.Resolve(grounded, new[] { grounded, grounded }, guideBounds) is null, "ambiguous controls rejected");
Assert(Guidance.Resolve(grounded, new[] { grounded with { X = 3000 } }, guideBounds) is null, "off-monitor control rejected");
Assert(Guidance.Requested("Show me settings"), "how-to question enables guidance");
Assert(CapturePreviewGeometry.Size(1080, 1920) == (360, 640), "portrait preview bounded");
Assert(CapturePreviewGeometry.Size(320, 200) == (320, 200), "small captures never enlarged");
var previewCapture = new CaptureDescriptor { OriginX = -1920, Width = 1920, Height = 1080,
    PreviewWidth = 640, PreviewHeight = 360 };
Assert(previewCapture.OriginX + 320 * previewCapture.PreviewScaleX == -960,
    "preview coordinates map to negative-origin physical display");
Assert(180 * previewCapture.PreviewScaleY == 540, "preview coordinates map vertical physical midpoint");
Assert(!System.Text.Json.JsonSerializer.Serialize(previewCapture).Contains("Preview"), "preview mapping is local-only");
Assert(WindowPlacement.Clamp(100, 100, 370, 136, 0, 0, 1920, 1040) == (100, 100), "inside work area");
Assert(WindowPlacement.Clamp(1900, 1020, 370, 136, 0, 0, 1920, 1040) == (1550, 904), "bottom/right clamping");
Assert(WindowPlacement.Clamp(-50, -50, 370, 136, 0, 0, 1920, 1040) == (0, 0), "top/left clamping");
Assert(WindowPlacement.Clamp(-1900, -100, 370, 136, -1920, -200, 1920, 1040) == (-1900, -100), "negative origin");
Assert(WindowPlacement.Clamp(100, 100, 370, 136, 0, 0, 200, 100) == (0, 0), "oversized window anchors without invalid clamp");
foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
{
    var rejected = false;
    try { WindowPlacement.Clamp(bad, 0, 370, 136, 0, 0, 1920, 1040); }
    catch (ArgumentOutOfRangeException) { rejected = true; }
    Assert(rejected, "non-finite layout rejected");
}

var state = new WhisperSessionStateMachine();
var privateRequest = WhisperRequestFactory.CreateWithCapture("hello", null, false,
    () => throw new InvalidOperationException("capture must not run without consent"),
    () => throw new InvalidOperationException("window lookup must not run without consent"));
Assert(privateRequest.Capture is null && privateRequest.ScreenshotBase64 is null,
    "monitor capture absent without consent");
Assert(privateRequest.ActiveWindow == "Unknown", "window metadata absent without consent");
var monitorCapture = new CaptureDescriptor { MonitorId = "display-2", OriginX = -1920,
    OriginY = -120, Width = 1920, Height = 1080 };
var captureCalls = 0;
var monitorRequest = WhisperRequestFactory.CreateWithCapture("look", null, true,
    () => { captureCalls++; return new ScreenSnapshot("AAAA", monitorCapture); }, () => "Editor");
Assert(captureCalls == 1 && monitorRequest.Capture == monitorCapture, "capture identity retained once");
Assert(monitorRequest.ScreenshotBase64 == "AAAA" && monitorRequest.ActiveWindow == "Editor", "consented capture metadata included");
Assert(monitorCapture.Bounds == new PixelBounds(-1920, -120, 1920, 1080), "negative physical monitor bounds retained");
var serializedCapture = System.Text.Json.JsonSerializer.Serialize(monitorCapture);
Assert(serializedCapture.Contains("capture_pixels") && !serializedCapture.Contains("Bounds"), "capture wire geometry explicit without duplicate bounds");
using (var assembler = new WebSocketTextAssembler())
{
    var unicode = System.Text.Encoding.UTF8.GetBytes("hello 🙂 नमस्ते");
    string? assembled = null;
    for (var index = 0; index < unicode.Length; index++)
    {
        assembled = assembler.Append(unicode.AsSpan(index, 1), index == unicode.Length - 1);
        if (index < unicode.Length - 1) Assert(assembled is null, "fragment is not prematurely decoded");
    }
    Assert(assembled == "hello 🙂 नमस्ते", "split UTF-8 emoji and Indic text preserved");
    Assert(assembler.Append(System.Text.Encoding.UTF8.GetBytes("next"), true) == "next", "assembler resets after complete message");
    var tooLarge = false;
    assembler.Append(new byte[] { 65 }, false);
    try { assembler.Append(new byte[WebSocketTextAssembler.MaximumBytes + 1], true); }
    catch (InvalidDataException) { tooLarge = true; }
    Assert(tooLarge, "oversized server event rejected");
    Assert(assembler.Append(new byte[] { 66 }, true) == "B", "oversized partial message resets buffer position");
    var invalidUtf8 = false;
    try { assembler.Append(new byte[] { 0xff }, true); } catch (System.Text.DecoderFallbackException) { invalidUtf8 = true; }
    Assert(invalidUtf8 && assembler.Append(new byte[] { 65 }, true) == "A", "invalid UTF-8 rejected and assembler reset");
}
Assert(CaptureBounds.PrimaryRegion(50, 50, 800, 600, 200, 100) == (0, 0, 200, 100), "capture shrinks to small display");
Assert(CaptureBounds.PrimaryRegion(int.MaxValue, int.MinValue, 800, 600, 1920, 1080) == (1120, 0, 800, 600), "capture bounds avoid integer overflow");
Assert(CaptureBounds.PrimaryRegion(960, 540, 800, 600, 1920, 1080) == (560, 240, 800, 600), "centered capture geometry");
var badRegion = false;
try { CaptureBounds.PrimaryRegion(0, 0, 0, 100, 1920, 1080); } catch (ArgumentOutOfRangeException) { badRegion = true; }
Assert(badRegion, "invalid capture dimensions rejected");
Assert(HighlightGeometry.ClipBox(10, 20, 50, 60, 100, 100) == (10, 20, 50, 60), "valid highlight geometry");
Assert(HighlightGeometry.ClipBox(-20, -10, 80, 60, 100, 100) == (0, 0, 60, 50), "clip negative model box");
Assert(HighlightGeometry.ClipBox(90, 95, 80, 60, 100, 100) == (90, 95, 10, 5), "clip overflowing model box");
Assert(HighlightGeometry.ClipBox(100, 0, 10, 10, 100, 100) is null, "reject outside model box");
Assert(HighlightGeometry.ClipBox(0, 0, -1, 10, 100, 100) is null, "reject negative model size");
Assert(HighlightGeometry.ClipBox(double.NaN, 0, 10, 10, 100, 100) is null, "reject nonfinite model geometry");
Assert(!state.TryTransition(WhisperState.Responding) && state.Current == WhisperState.Idle, "unsolicited response rejected");
Assert(state.TryTransition(WhisperState.TextInput), "open input");
Assert(state.TryTransition(WhisperState.TextInput), "repeated input is idempotent");
Assert(!state.TryTransition(WhisperState.Responding) && state.Current == WhisperState.TextInput, "late response during input rejected");
Assert(state.TryTransition(WhisperState.Thinking) && state.TryTransition(WhisperState.Responding), "submit/response lifecycle");
Assert(state.TryTransition(WhisperState.TextInput) && state.TryTransition(WhisperState.Idle), "open and cancel input during response");
Assert(state.TryTransition(WhisperState.Listening) && state.TryTransition(WhisperState.Idle), "cancel voice before release");
Assert(!state.TryTransition(WhisperState.Transcribing), "late voice-release cannot transcribe after dismissal");
using (var lifetime = new InteractionLifetime())
{
    Assert(lifetime.CurrentToken.IsCancellationRequested, "no turn means no permission to send");
    Assert(lifetime.TryBeginTurn(out var first) && !first.IsCancellationRequested, "begin active turn");
    Assert(lifetime.TryBeginTurn(out var second) && first.IsCancellationRequested, "new turn cancels old turn");
    Assert(lifetime.Pause() && second.IsCancellationRequested, "pause cancels in-flight work");
    Assert(!lifetime.Pause(), "pause is idempotent");
    Assert(!lifetime.TryBeginTurn(out var blocked) && blocked.IsCancellationRequested, "paused input is blocked");
    Assert(lifetime.Resume() && lifetime.CurrentToken.IsCancellationRequested, "resume does not restart capture");
    Assert(!lifetime.Resume(), "resume is idempotent");
    Assert(lifetime.TryBeginTurn(out var resumed) && !resumed.IsCancellationRequested, "explicit input after resume works");
    lifetime.CancelTurn();
    Assert(resumed.IsCancellationRequested, "dismiss cancels turn");
    lifetime.Dispose();
    Assert(!lifetime.TryBeginTurn(out _) && lifetime.IsPaused, "disposed instance cannot accept input");
}
state.Reset();
Assert(state.TryTransition(WhisperState.Listening) && state.TryTransition(WhisperState.Transcribing)
    && state.TryTransition(WhisperState.Listening), "new voice can interrupt transcription");
Assert(state.TryTransition(WhisperState.Transcribing) && state.TryTransition(WhisperState.TextInput),
    "text input can interrupt transcription");
var captures = 0;
var contexts = 0;
string Capture() { captures++; return "screenshot"; }
string Context() { contexts++; return "Private document title"; }
var textOnly = WhisperRequestFactory.Create("hello", null, false, Capture, Context);
Assert(captures == 0 && contexts == 0 && textOnly.ScreenshotBase64 is null && textOnly.ActiveWindow == "Unknown",
    "text without consent never invokes screen/context providers");
var screenQuestion = WhisperRequestFactory.Create("screen question", null, true, Capture, Context);
Assert(captures == 1 && contexts == 1 && screenQuestion.ScreenshotBase64 == "screenshot", "explicit screen consent invokes providers");
var settingsDirectory = Path.Combine(Path.GetTempPath(), $"whisper-settings-test-{Guid.NewGuid():N}");
Directory.CreateDirectory(settingsDirectory);
try
{
    var path = Path.Combine(settingsDirectory, "settings.json");
    var store = new SettingsStore(path);
    var initial = store.Load();
    Assert(!initial.Settings.IncludeScreenForVoice && initial.Warning is null, "first-run screen sharing is off");
    Assert(!File.Exists(path), "loading defaults does not write configuration");
    var custom = new UserSettings { SpeakResponses = false, ReducedMotion = true, IncludeScreenForVoice = true };
    store.Save(custom);
    Assert(store.Load().Settings == custom, "settings round-trip");
    store.Save(new UserSettings());
    Assert(File.Exists(path + ".bak") && !Directory.GetFiles(settingsDirectory, "*.tmp").Any(), "save preserves backup and cleans temporary files");
    File.WriteAllText(path, "invalid json");
    Assert(store.Load().Warning is not null && !store.Load().Settings.IncludeScreenForVoice, "invalid settings use safe defaults");
    Assert(File.ReadAllText(path) == "invalid json", "invalid settings are not overwritten on load");
    store.Save(new UserSettings());
    Assert(File.ReadAllText(path + ".bak") == "invalid json", "explicit repair preserves invalid original");
    File.WriteAllText(path, "{\"SchemaVersion\":2,\"IncludeScreenForVoice\":true}");
    Assert(store.Load().Warning is not null && !store.Load().Settings.IncludeScreenForVoice, "future settings use safe defaults");
    var futureRejected = false;
    try { store.Save(new UserSettings()); } catch (InvalidOperationException) { futureRejected = true; }
    Assert(futureRejected && File.ReadAllText(path).Contains("\"SchemaVersion\":2"), "future configuration cannot be downgraded");
}
finally { Directory.Delete(settingsDirectory, true); }
await AudioRecorderTests.RunAsync(Assert);
await RuntimeReadinessTests.RunAsync(Assert);
await DesktopAccountTests.RunAsync(Assert);
Assert(!Guidance.CanOffer(" SETTINGS ", new[] { "Settings" }, Array.Empty<string>()), "clicked labels do not repeat with different casing or spacing");
Assert(!Guidance.CanOffer("Settings", Array.Empty<string>(), new[] { "settings" }), "failed labels do not repeat");
Assert(Guidance.CanOffer("Privacy", new[] { "Settings" }, new[] { "Help" }), "different controls remain available");
Assert(!Guidance.Requested("How does this formula work?"), "explanation questions do not activate click guidance");
Assert(!Guidance.Requested("Explain these settings"), "incidental settings word is not navigation");
Assert(Guidance.Requested("Click Formulas"), "explicit navigation remains available");
Console.WriteLine($"Client core: {assertions} assertions passed.");
