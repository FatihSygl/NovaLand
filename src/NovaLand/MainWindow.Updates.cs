using System.Windows;
using System.Windows.Threading;
using NovaIsland.Services;

namespace NovaIsland;

public partial class MainWindow
{
    private readonly UpdateService _updates = new();
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private bool _updateBusy, _updateNotice, _updateNotified, _applyingUpdate;

    private void StartUpdates()
    {
        UpdateVersionText.Text = "NovaLand " + UpdateService.CurrentVersion;
        _updateTimer.Tick += async (_, _) =>
        {
            _updateTimer.Interval = TimeSpan.FromHours(6);
            await CheckUpdatesAsync(false);
        };
        _updateTimer.Start();
        // Commit only after the main window and its services have initialized.
        UpdateService.MarkSuccessfulStart();
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e) => await CheckUpdatesAsync(true);

    private async Task CheckUpdatesAsync(bool manual)
    {
        if (_updateBusy || _exiting) return;
        _updateBusy = true; CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "Güncellemeler kontrol ediliyor…";
        try
        {
            bool ready = await _updates.CheckAndDownloadAsync(message => { if (!_exiting) UpdateStatusText.Text = message; });
            if (_exiting) return;
            ApplyUpdateButton.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
            if (ready && !manual && !_updateNotified)
            {
                _updateNotified = true;
                EnqueueActivity("update-ready", 35, () =>
                {
                    ShowNoticeVisual("Yeni sürüm hazır", "Açmak için tıkla · " + _updates.ReadyVersion);
                    _updateNotice = true;
                });
            }
        }
        catch (OperationCanceledException) { if (!_exiting) UpdateStatusText.Text = "Bağlantı zaman aşımına uğradı. Tekrar deneyebilirsin."; }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Update check", ex);
            if (!_exiting) UpdateStatusText.Text = "Güncelleme alınamadı. Bağlantını kontrol edip tekrar dene.";
        }
        finally { _updateBusy = false; if (!_exiting) CheckUpdateButton.IsEnabled = true; }
    }

    private async void ApplyUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_applyingUpdate) return;
        _applyingUpdate = true; ApplyUpdateButton.IsEnabled = false;
        try { await _updates.StartReadyAsync(); Quit(); }
        catch (Exception ex) { DiagnosticLog.Write("Update start", ex); if (!_exiting) UpdateStatusText.Text = "Yeniden başlatılamadı. Mevcut sürüm çalışmaya devam ediyor."; }
        finally { _applyingUpdate = false; if (!_exiting) ApplyUpdateButton.IsEnabled = true; }
    }
}
