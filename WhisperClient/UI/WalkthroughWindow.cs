using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WhisperClient.Core;

namespace WhisperClient.UI;

/// <summary>Non-activating guide card: the user clicks the app, then requests the next step.</summary>
public sealed class WalkthroughWindow : Window
{
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr window, int index, int value);
    private readonly TextBlock _instruction = new() { Foreground = Brushes.White, FontSize = 14,
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
    private readonly Button _next = new() { Content = "Refresh", Visibility = Visibility.Collapsed, Padding = new Thickness(12, 7, 12, 7) };
    private readonly Button _failed = new() { Content = "Didn't work", Visibility = Visibility.Collapsed, Margin = new Thickness(10, 0, 0, 0), Padding = new Thickness(12, 7, 12, 7) };
    public event Action? NextRequested;
    public event Action? FailedRequested;
    public event Action? StopRequested;

    public WalkthroughWindow(int anchorX, int anchorY)
    {
        Title = "Whisper walkthrough"; Width = 380; SizeToContent = SizeToContent.Height;
        MaxHeight = 300; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
        ShowInTaskbar = false; ShowActivated = false;
        var panel = new StackPanel(); panel.Children.Add(_instruction);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(_next);
        buttons.Children.Add(_failed);
        var stop = new Button { Content = "Stop", Margin = new Thickness(10, 0, 0, 0), Padding = new Thickness(12, 7, 12, 7) };
        buttons.Children.Add(stop); panel.Children.Add(buttons);
        var style = (Style)System.Windows.Markup.XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Button">
                <Setter Property="Foreground" Value="#F2F4F8"/>
                <Setter Property="Background" Value="#333B4B"/>
                <Setter Property="FontSize" Value="12"/>
                <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
                    <Border x:Name="Surface" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                            CornerRadius="9" Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Surface" Property="Background" Value="#485771"/></Trigger>
                        <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.5"/></Trigger>
                        <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Surface" Property="BorderBrush" Value="White"/><Setter TargetName="Surface" Property="BorderThickness" Value="1"/></Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate></Setter.Value></Setter>
            </Style>
            """);
        _next.Style = style; stop.Style = style; _failed.Style = style;
        Content = new Border { Background = new SolidColorBrush(Color.FromRgb(24, 26, 32)), CornerRadius = new CornerRadius(16),
            BorderBrush = new SolidColorBrush(Color.FromRgb(65, 70, 82)), BorderThickness = new Thickness(1),
            Padding = new Thickness(16), Child = panel };
        _next.Click += (_, _) => { _next.IsEnabled = false; NextRequested?.Invoke(); };
        _failed.Click += (_, _) => { _failed.IsEnabled = false; FailedRequested?.Invoke(); };
        stop.Click += (_, _) => StopRequested?.Invoke();
        SourceInitialized += (_, _) =>
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x08000000 | 0x80);
            DisplayTopology.ExcludeFromCapture(handle);
        };
        Loaded += (_, _) => MonitorWindowPlacement.Place(new System.Windows.Interop.WindowInteropHelper(this).Handle, anchorX, anchorY);
    }

    public void SetStep(string text, bool next = true)
    { _instruction.Text = text; _next.IsEnabled = next; _next.Visibility = next ? Visibility.Visible : Visibility.Collapsed; _failed.IsEnabled = next; }

    public void EnableFailureFeedback(bool enabled)
    { _failed.IsEnabled = enabled; _failed.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed; }
}
