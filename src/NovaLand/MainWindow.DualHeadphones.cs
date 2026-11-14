using System.Windows;
using NovaIsland.Services;
using NovaIsland.Models;

namespace NovaIsland;

public partial class MainWindow
{
    private readonly DualHeadphoneService _dualHeadphones = new();
    private OutputAudioRow[] _secondHeadphoneRows = [];
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
        if (_settings.DualHeadphonesEnabled) SetTab("Music");
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
        (AudioOutput Output, BluetoothAudioSnapshot? Device)[] headphones = _settings.DualHeadphonesEnabled
            ? _lastAudioOutputs.Select(output => (Output: output, Device: FindBluetoothOutput(output)))
                .Where(entry => entry.Device is not null).ToArray()
            : [];
        var primary = headphones.FirstOrDefault(entry => entry.Output.Selected);
        var rows = headphones.Where(entry => !entry.Output.Selected && entry.Device!.Id != primary.Device?.Id)
            .GroupBy(entry => entry.Device!.Id)
            .Select(group => group.FirstOrDefault(entry => entry.Output.Id == _settings.SecondHeadphoneId) is { Device: not null } preferred
                ? preferred : group.First()).ToArray();
        bool visible = primary.Device is not null && rows.Length > 0;
        var chosen = rows.FirstOrDefault(entry => entry.Output.Id == _settings.SecondHeadphoneId);
        if (chosen.Device is null && rows.Length == 1) chosen = rows[0];
        string? target = visible ? chosen.Output?.Id : null;

        _primaryHeadphoneName = visible ? primary.Device!.Name : null;
        _secondHeadphoneRows = visible
            ? rows.Select(entry => new OutputAudioRow(entry.Output.Id, entry.Device!.Name, entry.Output.Id == target)).ToArray()
            : [];
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
    private BluetoothAudioSnapshot? FindBluetoothOutput(AudioOutput output)
    {
        var devices = _bluetooth.Connected;
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
