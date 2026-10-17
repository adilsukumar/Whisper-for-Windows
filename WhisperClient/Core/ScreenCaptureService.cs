using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace WhisperClient.Core;

/// <summary>Consented monitor capture, encoded as a bounded preview for CPU vision.</summary>
public sealed class ScreenCaptureService
{
    public ScreenSnapshot CaptureMonitor(DesktopMonitor monitor) => new(
        Capture(monitor.Bounds.Left, monitor.Bounds.Top, monitor.Bounds.Width, monitor.Bounds.Height),
        new CaptureDescriptor { MonitorId = monitor.DeviceName, OriginX = monitor.Bounds.Left,
            OriginY = monitor.Bounds.Top, Width = monitor.Bounds.Width, Height = monitor.Bounds.Height,
            PreviewWidth = CapturePreviewGeometry.Size(monitor.Bounds.Width, monitor.Bounds.Height).Width,
            PreviewHeight = CapturePreviewGeometry.Size(monitor.Bounds.Width, monitor.Bounds.Height).Height });
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(IntPtr destination,
        int dx, int dy, int width, int height, IntPtr source, int sx, int sy, int operation);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);

    public string CaptureFullScreenBase64() => Capture(0, 0, GetSystemMetrics(0), GetSystemMetrics(1));

    public string CaptureRegionBase64(int centerX, int centerY, int regionWidth = 800, int regionHeight = 600)
    {
        var region = CaptureBounds.PrimaryRegion(centerX, centerY, regionWidth, regionHeight,
            GetSystemMetrics(0), GetSystemMetrics(1));
        return Capture(region.X, region.Y, region.Width, region.Height);
    }

    private static string Capture(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > 40_000_000)
            throw new InvalidOperationException("Unsupported screen capture dimensions.");
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        var screen = GetDC(IntPtr.Zero);
        if (screen == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var destination = graphics.GetHdc();
            try
            {
                if (!BitBlt(destination, 0, 0, width, height, screen, x, y, 0x00CC0020))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally { graphics.ReleaseHdc(destination); }
        }
        finally { ReleaseDC(IntPtr.Zero, screen); }

        using var stream = new MemoryStream();
        var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);
        var size = CapturePreviewGeometry.Size(width, height);
        using var preview = new Bitmap(size.Width, size.Height, PixelFormat.Format24bppRgb);
        using (var previewGraphics = Graphics.FromImage(preview))
        {
            previewGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            previewGraphics.DrawImage(bitmap, 0, 0, size.Width, size.Height);
        }
        preview.Save(stream, encoder, parameters);
        return Convert.ToBase64String(stream.ToArray());
    }
}
