using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WhisperClient.Core;
using WhisperClient.Models;

namespace WhisperClient.UI;

/// <summary>One independent, non-activating annotation surface on the captured display.</summary>
public sealed class AnnotationWindow : Window
{
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr window, int index, int value);
    public AnnotationWindow(CaptureDescriptor capture, IEnumerable<ScreenHighlight> highlights)
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        Width = Height = 1;
        var canvas = new Canvas { ClipToBounds = true };
        Content = canvas;
        SourceInitialized += (_, _) =>
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x20 | 0x80 | 0x08000000);
            DisplayTopology.FitWindow(handle, capture.Bounds);
            DisplayTopology.ExcludeFromCapture(handle);
        };
        Loaded += (_, _) =>
        {
            foreach (var highlight in highlights.Take(32))
            {
                if (highlight.Type is not ("box" or "pulse")) continue;
                var origin = PointFromScreen(new Point(capture.OriginX + highlight.X * capture.PreviewScaleX,
                    capture.OriginY + highlight.Y * capture.PreviewScaleY));
                var corner = PointFromScreen(new Point(capture.OriginX + (highlight.X + highlight.Width) * capture.PreviewScaleX,
                    capture.OriginY + (highlight.Y + highlight.Height) * capture.PreviewScaleY));
                Shape shape;
                double x = origin.X, y = origin.Y;
                if (highlight.Type == "box")
                {
                    var box = HighlightGeometry.ClipBox(x, y, corner.X - x, corner.Y - y, ActualWidth, ActualHeight);
                    if (box is null) continue;
                    (x, y, var width, var height) = box.Value;
                    shape = new Rectangle { Width = width, Height = height, Stroke = Brushes.DeepSkyBlue,
                        StrokeThickness = 2.5, Fill = new SolidColorBrush(Color.FromArgb(30, 64, 169, 255)) };
                }
                else
                {
                    if (x < 0 || y < 0 || x >= ActualWidth || y >= ActualHeight) continue;
                    shape = new Ellipse { Width = 30, Height = 30, Fill = Brushes.Coral };
                    x -= 15; y -= 15;
                }
                Canvas.SetLeft(shape, x); Canvas.SetTop(shape, y); canvas.Children.Add(shape);
                if (!string.IsNullOrWhiteSpace(highlight.Label))
                {
                    var label = new TextBlock { Text = highlight.Label[..Math.Min(200, highlight.Label.Length)],
                        Foreground = Brushes.White, Background = Brushes.Black, Padding = new Thickness(4), MaxWidth = 300, TextWrapping = TextWrapping.Wrap };
                    Canvas.SetLeft(label, Math.Max(0, x)); Canvas.SetTop(label, Math.Max(0, y - 26));
                    canvas.Children.Add(label);
                }
            }
        };
    }
}
