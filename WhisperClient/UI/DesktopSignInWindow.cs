using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using WhisperClient.Core;
namespace WhisperClient.UI;
public sealed class DesktopSignInWindow : Window
{
    private readonly DesktopAccountClient _accounts;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12) };
    private readonly Button _start = new() { Content = "Sign in with Google", Padding = new Thickness(12) };
    public DesktopSignInWindow(DesktopAccountClient accounts)
    {
        _accounts = accounts; Title = "Whisper account"; Width = 440; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Sign in to Whisper", FontSize = 24 });
        _status.Text = "Paid release: a verified paid or complimentary account is required. Sign-in uses your browser; Whisper never requests your Google password.";
        panel.Children.Add(_status); panel.Children.Add(_start);
        if (accounts.AccessToken is not null)
        {
            var logout = new Button { Content = "Sign out", Margin = new Thickness(0, 10, 0, 0) };
            logout.Click += async (_, _) =>
            {
                logout.IsEnabled = false;
                try { await accounts.LogoutAsync(_lifetime.Token); _status.Text = "Signed out. Sign in again to use Whisper."; }
                catch (Exception) { if (!_lifetime.IsCancellationRequested) _status.Text = "Signed out locally. Server revocation was unavailable; the session expires within one hour."; }
            };
            panel.Children.Add(logout);
        }
        Content = panel;
        _start.Click += StartSignIn;
        Closed += (_, _) => _lifetime.Cancel();
    }
    private async void StartSignIn(object sender, RoutedEventArgs e)
    {
        _start.IsEnabled = false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromMinutes(4));
        try
        {
            var approval = await _accounts.StartAsync(timeout.Token);
            _status.Text = $"Confirm this code in the browser: {approval.DisplayCode}\nOnly approve a sign-in you initiated. Waiting for approval…";
            Process.Start(new ProcessStartInfo(approval.ApprovalUrl.AbsoluteUri) { UseShellExecute = true });
            while (!await _accounts.RedeemAsync(approval, timeout.Token))
                await Task.Delay(TimeSpan.FromSeconds(3), timeout.Token);
            DialogResult = true;
        }
        catch (OperationCanceledException) { if (!_lifetime.IsCancellationRequested) _status.Text = "Sign-in timed out. Try again."; }
        catch (Exception) { if (!_lifetime.IsCancellationRequested) _status.Text = "Sign-in could not be verified. Check your connection and account access, then try again."; }
        finally { if (!_lifetime.IsCancellationRequested) _start.IsEnabled = true; }
    }
}
