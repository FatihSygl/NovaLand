using System.Windows;
using NovaIsland.Services;
namespace NovaIsland;
public partial class MainWindow
{
    private bool _screenshotBusy;
    private void InitializeClipboardCapture()
    {
        if (_clipboard is not null) return;
        try
        {
            _clipboard = new ClipboardService { Enabled = _settings.CaptureClipboard };
            _clipboard.FilesReceived += paths => _documents.Add(paths);
            _clipboard.TextReceived += text => _clipboardHistory.Add(text);
            _clipboard.ImageReceived += png => _images.Add(png, "Kopyalanan görsel", DateTime.Now);
            if (!_clipboard.Listening) StatusText.Text = "Pano takibi yedek yöntemle çalışıyor.";
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Clipboard initialization", ex);
            StatusText.Text = "Pano takibi başlatılamadı. Uygulamayı yeniden başlatmayı dene.";
        }
        RefreshClipboardCaptureState();
    }
    private void RefreshClipboardCaptureState()
    {
        ClipboardCaptureNotice.Visibility = !_settings.CaptureClipboard ? Visibility.Visible : Visibility.Collapsed;
    }
    private void EnableClipboardCapture_Click(object sender, RoutedEventArgs e) => CaptureClipboardCheck.IsChecked = true;
    private async Task ScanScreenshotsAsync()
    {
        if (_screenshotBusy || !_settings.CaptureClipboard || _exiting) return;
        _screenshotBusy = true;
        try { await _screenshots.ScanAsync(); }
        catch (Exception ex) { DiagnosticLog.Write("Screenshot history", ex); }
        finally { _screenshotBusy = false; }
    }
}
