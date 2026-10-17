using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using WhisperClient.Core;

namespace WhisperClient.Services;

/// <summary>Opt-in visible accessibility labels, never password/edit values.</summary>
public static class ScreenTextContextService
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    private static int _reading;

    // An unresponsive accessibility provider must not block the UI or spawn
    // an unbounded number of background workers on repeated questions.
    public static async Task<string?> ReadAsync(IntPtr window, PixelBounds monitor, CancellationToken cancellation)
        => (await ReadSnapshotAsync(window, monitor, cancellation))?.Text;

    public static async Task<ScreenTextSnapshot?> ReadSnapshotAsync(IntPtr window, PixelBounds monitor, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (window == IntPtr.Zero || Interlocked.CompareExchange(ref _reading, 1, 0) != 0) return null;
        var result = new TaskCompletionSource<ScreenTextSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new Thread(() =>
        {
            try { result.TrySetResult(Read(window, monitor)); }
            catch { result.TrySetResult(null); }
            finally { Interlocked.Exchange(ref _reading, 0); }
        }) { IsBackground = true, Name = "Whisper screen text" };
        worker.SetApartmentState(ApartmentState.MTA);
        worker.Start();
        try { return await result.Task.WaitAsync(TimeSpan.FromMilliseconds(750), cancellation); }
        catch (TimeoutException) { return null; }
    }

    private static ScreenTextSnapshot? Read(IntPtr window, PixelBounds monitor)
    {
        if (!IsWindowVisible(window) || !GetWindowRect(window, out var rect)) return null;
        GetWindowThreadProcessId(window, out var process);
        if (process == Environment.ProcessId || rect.Right <= monitor.Left || rect.Left >= monitor.Left + monitor.Width
            || rect.Bottom <= monitor.Top || rect.Top >= monitor.Top + monitor.Height) return null;
        var root = AutomationElement.FromHandle(window);
        var queue = new Queue<AutomationElement>(); queue.Enqueue(root);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var text = new StringBuilder();
        var targets = new List<ScreenTarget>();
        var clock = Stopwatch.StartNew(); var nodes = 0;
        var walker = TreeWalker.ControlViewWalker;
        while (queue.Count > 0 && nodes++ < 160 && clock.ElapsedMilliseconds < 600 && text.Length < 3000)
        {
            var element = queue.Dequeue();
            var current = element.Current;
            // Never read editable values or descend into password/edit controls.
            if (current.IsPassword || current.ControlType == ControlType.Edit || current.IsOffscreen) continue;
            var bounds = current.BoundingRectangle;
            var inside = !bounds.IsEmpty && bounds.Left >= monitor.Left && bounds.Top >= monitor.Top
                && bounds.Right <= monitor.Left + monitor.Width && bounds.Bottom <= monitor.Top + monitor.Height;
            var type = current.ControlType;
            if (inside && current.IsEnabled && (type == ControlType.Button
                || type == ControlType.MenuItem || type == ControlType.TabItem || type == ControlType.Hyperlink
                || type == ControlType.CheckBox || type == ControlType.ListItem || type == ControlType.TreeItem
                || type == ControlType.ComboBox))
            {
                var label = current.Name?.Trim();
                if (!string.IsNullOrWhiteSpace(label) && label.Length <= 200 && bounds.Width >= 2 && bounds.Height >= 2)
                {
                    string? state = null;
                    if (type == ControlType.CheckBox && element.TryGetCurrentPattern(TogglePattern.Pattern, out var toggle))
                        state = ((TogglePattern)toggle).Current.ToggleState switch { ToggleState.On => "on", ToggleState.Off => "off", _ => "indeterminate" };
                    targets.Add(new ScreenTarget { Id = $"t{targets.Count}", Label = label, State = state,
                        Priority = type == ControlType.MenuItem ? 0 : type == ControlType.Button ? 1 : type == ControlType.TabItem ? 3 : 2,
                        RuntimeId = string.Join(".", element.GetRuntimeId()), X = (int)Math.Ceiling(bounds.Left),
                        Y = (int)Math.Ceiling(bounds.Top), Width = (int)Math.Floor(bounds.Width), Height = (int)Math.Floor(bounds.Height) });
                }
            }
            if (inside && (type == ControlType.Text || type == ControlType.Button || type == ControlType.Hyperlink
                || type == ControlType.ListItem || type == ControlType.MenuItem || type == ControlType.TabItem
                || type == ControlType.CheckBox))
            {
                var name = current.Name?.Trim();
                if (!string.IsNullOrWhiteSpace(name) && seen.Add(name))
                    text.AppendLine(name[..Math.Min(name.Length, Math.Min(600, 3000 - text.Length))]);
            }
            var child = walker.GetFirstChild(element);
            while (child is not null && queue.Count + nodes < 160 && clock.ElapsedMilliseconds < 600)
            {
                queue.Enqueue(child); child = walker.GetNextSibling(child);
            }
        }
        return text.Length >= 80 || targets.Count > 0
            ? new ScreenTextSnapshot(text.ToString()[..Math.Min(3000, text.Length)],
                targets.OrderBy(target => target.Priority).Take(24).Select((target, index) => target with { Id = $"t{index}" }).ToArray()) : null;
    }
}
