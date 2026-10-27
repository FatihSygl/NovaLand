using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace NovaIsland;
public partial class MainWindow
{
    private readonly DispatcherTimer _statusExpiry = new() { Interval = TimeSpan.FromSeconds(4) };
    private DependencyPropertyDescriptor? _statusDescriptor;
    private void InitializeStatusFeedback()
    {
        _statusExpiry.Tick += (_, _) => { _statusExpiry.Stop(); StatusText.Text = ""; };
        _statusDescriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        _statusDescriptor.AddValueChanged(StatusText, StatusChanged);
        Closed += (_, _) => { _statusExpiry.Stop(); _statusDescriptor.RemoveValueChanged(StatusText, StatusChanged); };
        StatusText.Text = "";
    }
    private void StatusChanged(object? sender, EventArgs e)
    {
        _statusExpiry.Stop();
        if (!string.IsNullOrEmpty(StatusText.Text)) _statusExpiry.Start();
    }
}
