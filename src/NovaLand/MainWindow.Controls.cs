using System.Windows;
using System.Windows.Media.Animation;
using NovaIsland.Models;
using NovaIsland.Services;

namespace NovaIsland;

public partial class MainWindow
{
    private bool _outputBusy;
    private bool _headphoneChangeBusy;
    private ConnectedAudioRow[] _shownConnected = [];
    private OutputAudioRow[] _shownOutputs = [];
    private sealed record ConnectedAudioRow(string Name, int? Battery, ulong Address, DisabledHeadphone? Disabled = null)
    {
        public string ConnectionLabel => Disabled is null ? "Bağlı" : "Devre dışı";
        public string ConnectionColor => Disabled is null ? "#87A180" : "#96969C";
        public string ActionLabel => Disabled is null ? "Devre dışı bırak" : "Etkinleştir";
        public string ActionHint => Disabled is null ? "Eşleştirmeyi koruyarak bu bilgisayardaki bağlantıyı kapat" : "Bu bilgisayardaki bağlantıyı yeniden etkinleştir";
        public Visibility ActionVisibility => Address != 0 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility BatteryVisibility => Disabled is null ? Visibility.Visible : Visibility.Collapsed;
        public string BatteryLabel => Battery.HasValue ? $"%{Math.Clamp(Battery.Value, 0, 100)}" : "Pil bilgisi yok";
        public Visibility BatteryIconVisibility => Battery.HasValue ? Visibility.Visible : Visibility.Collapsed;
        public string BatteryHint => Battery.HasValue ? $"Kulaklık pili: {BatteryLabel}" : "Pil bilgisi yok";
        public double BatteryFillWidth => Battery.HasValue ? 17 * Math.Clamp(Battery.Value, 0, 100) / 100.0 : 0;
        public string BatteryColor => Battery is null ? "#777780" : Battery is <= 10 ? "#FF5B5D" : Battery is <= 20 ? "#FFB45B" : "#B9F879";
    }
    private sealed record OutputAudioRow(string Id, string Name, bool Selected)
    {
        public Visibility SelectedVisibility => Selected ? Visibility.Visible : Visibility.Collapsed;
    }
    private void RefreshConnectedAudio()
    {
        if (_headphoneChangeBusy) return;
        var disabled = _settings.DisabledHeadphones ?? [];
        var rows = _bluetooth.Connected
            .Where(x => !disabled.Any(device => device.Address == x.Address))
            .Select(x => new ConnectedAudioRow(x.Name, x.Battery, x.Address))
            .Concat(disabled.Select(x => new ConnectedAudioRow(x.Name, null, x.Address, x)))
            .ToArray();
        if (!_shownConnected.SequenceEqual(rows)) ConnectedAudioList.ItemsSource = _shownConnected = rows;
        ConnectedAudioEmpty.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void HeadphoneConnection_Click(object sender, RoutedEventArgs e)
    {
        if (_headphoneChangeBusy || _exiting ||
            (sender as FrameworkElement)?.DataContext is not ConnectedAudioRow row || row.Address == 0)
            return;

        _headphoneChangeBusy = true;
        ConnectedAudioList.IsEnabled = false;
        var disabled = _settings.DisabledHeadphones ??= [];
        var saved = row.Disabled;
        bool enabling = saved is not null;
        bool keepRecoveryEntry = false;
        try
        {
            if (saved is null)
            {
                string? instanceId = await Task.Run(() => BluetoothBatteryReader.FindClassicDevice(row.Address));
                if (_exiting) return;
                if (instanceId is null)
                {
                    StatusText.Text = "Kulaklığın Windows aygıt kaydı bulunamadı.";
                    return;
                }

                // Save the enable action first so it survives an exit during the Windows prompt.
                saved = new DisabledHeadphone(row.Address, instanceId, row.Name);
                disabled.Add(saved);
                SettingsStore.Save(_settings);
                keepRecoveryEntry = true;
            }

            var device = saved;
            var result = await Task.Run(() => BluetoothDeviceSwitch.SetEnabled(device.Address, device.InstanceId, enabling));
            if (result is BluetoothDeviceChange.Applied or BluetoothDeviceChange.RestartRequired)
            {
                if (enabling && result == BluetoothDeviceChange.Applied)
                {
                    disabled.Remove(device);
                    SaveSettings();
                }

                if (_exiting) return;
                StatusText.Text = result == BluetoothDeviceChange.RestartRequired
                    ? "Değişikliği tamamlamak için Windows’u yeniden başlat."
                    : enabling ? "Kulaklık etkinleştirildi." : "Kulaklık devre dışı bırakıldı.";
                _ = ScanDevicesAsync();
            }
            else
            {
                keepRecoveryEntry = false;
                if (!_exiting)
                    StatusText.Text = result == BluetoothDeviceChange.Cancelled
                        ? "İşlem iptal edildi." : "Windows kulaklığın durumunu değiştiremedi.";
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("HeadphoneConnectionChange", ex);
            if (!_exiting) StatusText.Text = "Kulaklığın durumu değiştirilemedi.";
        }
        finally
        {
            if (!enabling && !keepRecoveryEntry && saved is not null)
            {
                disabled.Remove(saved);
                SaveSettings();
            }
            _headphoneChangeBusy = false;
            if (!_exiting)
            {
                ConnectedAudioList.IsEnabled = true;
                RefreshConnectedAudio();
            }
        }
    }

    private void ConnectedBatteryFill_Loaded(object sender, RoutedEventArgs e) => AnimateConnectedBattery(sender);
    private void ConnectedBatteryFill_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true) AnimateConnectedBattery(sender);
    }
    private static void AnimateConnectedBattery(object sender)
    {
        if (sender is not FrameworkElement { IsLoaded: true, IsVisible: true, DataContext: ConnectedAudioRow row } fill) return;
        fill.BeginAnimation(FrameworkElement.WidthProperty, null);
        if (!row.Battery.HasValue) return;
        fill.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(0, row.BatteryFillWidth, TimeSpan.FromMilliseconds(650))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        });
    }
    private async Task RefreshOutputAudioAsync()
    {
        if (_outputBusy || _exiting) return;
        _outputBusy = true;
        try { var rows = await Task.Run(AudioOutputService.Read); if (!_exiting) ShowOutputAudio(rows); }
        catch (Exception ex) { DiagnosticLog.Write("OutputList", ex); OutputAudioEmpty.Text = "Ses çıkışları okunamadı. Windows ses ayarlarını açabilirsin."; OutputAudioEmpty.Visibility = Visibility.Visible; }
        finally { _outputBusy = false; }
    }
    private void ShowSystemVolume(int? volume)
    {
        VolumeSlider.IsEnabled = volume.HasValue;
        VolumeValue.Text = volume is { } percentage ? $"{percentage}%" : "–";
        if (volume is not { } level || Math.Abs(VolumeSlider.Value - level) < 1) return;
        bool wasReady = _ready;
        _ready = false;
        VolumeSlider.Value = level;
        _ready = wasReady;
    }

    private void ShowOutputAudio(AudioOutput[] rows)
    {
        var items = rows.Select(x => new OutputAudioRow(x.Id, x.Name, x.Selected)).ToArray();
        if (!_shownOutputs.SequenceEqual(items)) OutputAudioList.ItemsSource = _shownOutputs = items;
        OutputAudioEmpty.Text = "Ses çıkışı bulunamadı.";
        OutputAudioEmpty.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowSecondHeadphoneOutputs(rows);
    }
    private async void OutputAudio_Click(object sender, RoutedEventArgs e)
    {
        if (_outputBusy || (sender as FrameworkElement)?.DataContext is not OutputAudioRow row || row.Selected) return;
        _outputBusy = true; OutputAudioList.IsEnabled = false;
        try
        {
            bool changed = await Task.Run(() => AudioOutputService.Select(row.Id));
            if (_exiting) return;
            StatusText.Text = changed ? "Ses çıkışı değiştirildi." : "Ses çıkışı değiştirilemedi. Windows ses ayarlarını kullanabilirsin.";
            ShowOutputAudio(await Task.Run(AudioOutputService.Read));
            var volume = AudioService.ReadVolume();
            ShowSystemVolume(volume);
        }
        catch (Exception ex) { DiagnosticLog.Write("OutputChange", ex); StatusText.Text = "Ses çıkışı okunamadı. Windows ses ayarlarını kullanabilirsin."; }
        finally { _outputBusy = false; OutputAudioList.IsEnabled = true; }
    }
}
