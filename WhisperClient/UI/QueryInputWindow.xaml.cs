using System.Windows;
using System.Windows.Input;
using WhisperClient.Core;

namespace WhisperClient.UI;

public partial class QueryInputWindow : Window
{
    public event Action<string, bool>? Submitted;

    public QueryInputWindow(int physicalX, int physicalY)
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            try { MonitorWindowPlacement.Place(new System.Windows.Interop.WindowInteropHelper(this).Handle, physicalX, physicalY); }
            catch (System.ComponentModel.Win32Exception)
            {
                // A disappearing monitor/native failure must not prevent keyboard input.
                var area = SystemParameters.WorkArea;
                Left = area.Left; Top = area.Top;
            }
            Activate(); QueryTextBox.Focus();
        };
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(QueryTextBox.Text))
        {
            e.Handled = true;
            Submitted?.Invoke(QueryTextBox.Text.Trim(), IncludeScreen.IsChecked == true);
        }
    }

}
