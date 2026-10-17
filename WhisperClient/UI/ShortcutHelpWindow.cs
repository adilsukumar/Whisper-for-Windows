using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WhisperClient.UI;

public sealed class ShortcutHelpWindow : Window
{
    public ShortcutHelpWindow(bool helpAvailable, bool quitAvailable)
    {
        Title = "Whisper controls";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var body = new StackPanel { Margin = new Thickness(24) };
        body.Children.Add(new TextBlock { Text = "Whisper controls", FontSize = 22,
            FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 18) });
        AddRow(body, "Ctrl + Space (hold)", "Speak; release to send");
        AddRow(body, "Ctrl + Shift + Space", "Type a question");
        AddRow(body, "Ctrl + Shift + H", "Show these controls");
        AddRow(body, "Ctrl + Shift + Q", "Quit Whisper completely");
        AddRow(body, "Esc", "Dismiss / stop the current interaction");
        AddRow(body, "Enter", "Send your typed question");
        body.Children.Add(new TextBlock { Text = "Tick Ask about my screen to share screen context.\nPause / Resume and Settings are in the tray menu.\nQuitting closes the desktop app; Ollama stays running.",
            Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 14, 0, 14), FontSize = 13 });
        if (!helpAvailable || !quitAvailable)
            body.Children.Add(new TextBlock { Text = "Shortcut unavailable: " +
                (!helpAvailable ? "Ctrl+Shift+H " : "") + (!quitAvailable ? "Ctrl+Shift+Q " : "") +
                "— another app may already use it. Use the tray menu instead.",
                Foreground = Brushes.Khaki, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14) });
        var close = new Button { Content = "Close", Padding = new Thickness(16, 7, 16, 7),
            HorizontalAlignment = HorizontalAlignment.Right, IsCancel = false };
        close.Click += (_, _) => Close();
        body.Children.Add(close);
        Content = new Border { CornerRadius = new CornerRadius(18), Background = new SolidColorBrush(Color.FromRgb(25, 28, 36)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(65, 70, 85)), BorderThickness = new Thickness(1), Child = body };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is TextBlock) DragMove(); };
    }

    private static void AddRow(Panel body, string keys, string action)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        var key = new TextBlock { Text = keys, Foreground = Brushes.White, FontWeight = FontWeights.Medium, FontSize = 13 };
        var description = new TextBlock { Text = action, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap, FontSize = 13 };
        Grid.SetColumn(description, 1);
        row.Children.Add(key);
        row.Children.Add(description);
        body.Children.Add(row);
    }
}
