using System.Windows;
using NovaIsland.Services;
using NovaIsland.Models;

namespace NovaIsland;

public partial class MainWindow
{
    private readonly DualHeadphoneService _dualHeadphones = new();
    private OutputAudioRow[] _secondHeadphoneRows = [];
    private HeadphoneSettingsRow[] _shownHeadphoneSettings = [];

    private sealed record HeadphoneSettingsRow(string Name, ulong Address, string? OutputId,
        bool IsPrimary, bool IsSecondary, DisabledHeadphone? Disabled = null)
    {
        public Visibility RoleVisibility => OutputId is null ? Visibility.Collapsed : Visibility.Visible;
        public Visibility ConnectionVisibility => Address == 0 ? Visibility.Collapsed : Visibility.Visible;
        public string ConnectionAction => Disabled is null ? "Devre dışı bırak" : "Etkinleştir";
        public string ConnectionState => Disabled is null ? "Bağlı" : "Devre dışı";
    }

    private AudioOutput[] _lastAudioOutputs = [];
    private bool _updatingSecondVolume;
    private string? _sharedHeadphoneId;
    private string? _primaryHeadphoneId;
    private string? _primaryHeadphoneName;

    private void DualHeadphones_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _settings.DualHeadphonesEnabled = DualHeadphonesCheck.IsChecked == true;
        SaveSettings();
        ApplyDualHeadphonesSettings();
        if (_settings.DualHeadphonesEnabled) _ = RefreshOutputAudioAsync();
    }

    private void SecondHeadphoneVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready || _updatingSecondVolume || !_settings.DualHeadphonesEnabled) return;
        int volume = (int)Math.Round(e.NewValue);
        SecondHeadphoneVolumeText.Text = $"{volume}%";
        _dualHeadphones.SetSecondVolume(volume);
    }

    private void ShowSecondHeadphoneOutputs(AudioOutput[] outputs)
    {
        _lastAudioOutputs = outputs;
        ApplyDualHeadphonesSettings();
    }

    private void ApplyDualHeadphonesSettings()
    {
        if (_headphoneChangeBusy && _settings.DualHeadphonesEnabled) return;
        var disabled = _settings.DisabledHeadphones ?? [];
        BluetoothAudioSnapshot[] devices = _settings.DualHeadphonesEnabled
            ? _bluetooth.Connected.Where(device => !disabled.Any(item => item.Address == device.Address)).ToArray()
            : [];
        var headphones = _lastAudioOutputs.Select(output => (Output: output, Device: FindBluetoothOutput(output, devices)))
            .Where(entry => entry.Device is not null)
            .GroupBy(entry => entry.Device!.Id)
            .Select(group => group.FirstOrDefault(entry => entry.Output.Selected) is { Device: not null } selected
                ? selected : group.FirstOrDefault(entry => entry.Output.Id == _settings.SecondHeadphoneId) is { Device: not null } second
                    ? second : group.First()).ToArray();
        var primary = headphones.FirstOrDefault(entry => entry.Output.Selected);
        var rows = headphones.Where(entry => !entry.Output.Selected).ToArray();
        var chosen = rows.FirstOrDefault(entry => entry.Output.Id == _settings.SecondHeadphoneId);
        if (primary.Device is not null && _settings.SecondHeadphoneId is null && rows.Length == 1) chosen = rows[0];
        string? target = primary.Device is null ? null : chosen.Output?.Id;

        RefreshHeadphoneSettings(devices, headphones, chosen.Output?.Id);
        _primaryHeadphoneName = target is null ? null : primary.Device!.Name;
        _secondHeadphoneRows = primary.Device is null
            ? [] : rows.Select(entry => new OutputAudioRow(entry.Output.Id, entry.Device!.Name, entry.Output.Id == target)).ToArray();
        if (target is null) DualHeadphonesStatus.Text = "";

        string? source = target is null ? null : primary.Output.Id;
        if (_sharedHeadphoneId == target && _primaryHeadphoneId == source)
        {
            RefreshQuickSite();
            return;
        }

        _sharedHeadphoneId = target;
        _primaryHeadphoneId = source;
        RefreshQuickSite();
        SecondHeadphoneVolume.IsEnabled = false;
        SecondHeadphoneVolumeText.Text = "–";
        if (target is not null) DualHeadphonesStatus.Text = "Çıkış bağlanıyor…";

        _dualHeadphones.SetTarget(source, target, (message, token) =>
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                if (!_exiting && !token.IsCancellationRequested)
                    DualHeadphonesStatus.Text = message.StartsWith("Ses paylaşılıyor ·", StringComparison.Ordinal)
                        ? "" : message;
            });
        }, (level, revision, token) =>
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                if (_exiting || token.IsCancellationRequested) return;
                SecondHeadphoneVolume.IsEnabled = level.HasValue;
                if (level is not { } current)
                {
                    SecondHeadphoneVolumeText.Text = "–";
                    return;
                }
                if (revision != _dualHeadphones.VolumeRevision) return;

                _updatingSecondVolume = true;
                SecondHeadphoneVolume.Value = current;
                _updatingSecondVolume = false;
                SecondHeadphoneVolumeText.Text = $"{current}%";
            });
        });
    }
    private void RefreshHeadphoneSettings(BluetoothAudioSnapshot[] devices,
        (AudioOutput Output, BluetoothAudioSnapshot? Device)[] outputs, string? target)
    {
        if (!_settings.DualHeadphonesEnabled) return;
        var rows = devices.Select(device =>
        {
            var output = outputs.FirstOrDefault(entry => entry.Device?.Id == device.Id).Output;
            return new HeadphoneSettingsRow(device.Name, device.Address, output?.Id,
                output?.Selected == true, target is not null && output?.Id == target);
        }).Concat((_settings.DisabledHeadphones ?? []).Select(device =>
            new HeadphoneSettingsRow(device.Name, device.Address, null, false, false, device))).ToArray();
        if (!_shownHeadphoneSettings.SequenceEqual(rows)) HeadphoneSettingsList.ItemsSource = _shownHeadphoneSettings = rows;
        HeadphoneSettingsEmpty.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void PrimaryHeadphone_Click(object sender, RoutedEventArgs e)
    {
        if (!_settings.DualHeadphonesEnabled ||
            (sender as FrameworkElement)?.DataContext is not HeadphoneSettingsRow { OutputId: { } id, IsPrimary: false } row) return;

        string? previousPrimary = _shownHeadphoneSettings.FirstOrDefault(device => device.IsPrimary)?.OutputId;
        await ChangeOutputAudioAsync(id, () =>
        {
            if (!row.IsSecondary && _settings.SecondHeadphoneId != id) return;
            _settings.SecondHeadphoneId = previousPrimary;
            SaveSettings();
        });
    }

    private void SecondaryHeadphone_Click(object sender, RoutedEventArgs e)
    {
        if (!_settings.DualHeadphonesEnabled || _headphoneChangeBusy || _outputBusy ||
            (sender as FrameworkElement)?.DataContext is not HeadphoneSettingsRow { OutputId: { } id, IsPrimary: false }) return;
        _settings.SecondHeadphoneId = id;
        SaveSettings();
        ApplyDualHeadphonesSettings();
    }

    private async void HeadphoneConnection_Click(object sender, RoutedEventArgs e)
    {
        if (!_settings.DualHeadphonesEnabled || _headphoneChangeBusy || _outputBusy || _exiting ||
            (sender as FrameworkElement)?.DataContext is not HeadphoneSettingsRow row || row.Address == 0)
            return;

        _headphoneChangeBusy = true;
        HeadphoneSettingsList.IsEnabled = false;
        var disabled = _settings.DisabledHeadphones ??= [];
        var saved = row.Disabled;
        bool enabling = saved is not null;
        bool keepRecoveryEntry = false;
        try
        {
            if (saved is null)
            {
                string? instanceId = await Task.Run(() => BluetoothBatteryReader.FindClassicDevice(row.Address));
                if (_exiting || !_settings.DualHeadphonesEnabled) return;
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
                HeadphoneSettingsList.IsEnabled = true;
                ApplyDualHeadphonesSettings();
            }
        }
    }

    private static BluetoothAudioSnapshot? FindBluetoothOutput(AudioOutput output, BluetoothAudioSnapshot[] devices)
    {
        if (output.Container != Guid.Empty)
        {
            var match = devices.FirstOrDefault(device => device.Container == output.Container);
            if (match is not null) return match;
        }

        // Driver-dependent fallback for endpoints that omit or report a different container.
        var matches = devices.Where(device => device.Name.Length >= 3 && device.Name != "Bluetooth ses cihazı" &&
            (string.Equals(output.Name, device.Name, StringComparison.OrdinalIgnoreCase) ||
             output.Name.Contains($"({device.Name})", StringComparison.OrdinalIgnoreCase))).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
}
