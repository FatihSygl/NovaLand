using System.Windows;
using NovaIsland.Services;

namespace NovaIsland;

public partial class MainWindow
{
    private readonly DualHeadphoneService _dualHeadphones = new();
    private OutputAudioRow[] _secondHeadphoneRows = [];
    private AudioOutput[] _lastAudioOutputs = [];

    private void DualHeadphones_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _settings.DualHeadphonesEnabled = DualHeadphonesCheck.IsChecked == true;
        SaveSettings();
        ApplyDualHeadphonesSettings();
        if (_settings.DualHeadphonesEnabled) _ = RefreshOutputAudioAsync();
    }

    private void SecondHeadphone_Click(object sender, RoutedEventArgs e)
    {
        if (!_settings.DualHeadphonesEnabled || sender is not FrameworkElement { DataContext: OutputAudioRow row } ||
            _settings.SecondHeadphoneId == row.Id) return;

        _settings.SecondHeadphoneId = row.Id;
        SaveSettings();
        ApplyDualHeadphonesSettings();
    }

    private void ShowSecondHeadphoneOutputs(AudioOutput[] outputs)
    {
        _lastAudioOutputs = outputs;
        if (!_settings.DualHeadphonesEnabled) return;
        var rows = outputs.Where(output => !output.Selected)
            .Select(output => new OutputAudioRow(output.Id, output.Name, output.Id == _settings.SecondHeadphoneId))
            .ToArray();
        if (!_secondHeadphoneRows.SequenceEqual(rows))
            DualHeadphonesOutputs.ItemsSource = _secondHeadphoneRows = rows;
    }

    private void ApplyDualHeadphonesSettings()
    {
        bool enabled = _settings.DualHeadphonesEnabled;
        string? target = enabled ? _settings.SecondHeadphoneId : null;
        DualHeadphonesPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        if (enabled)
        {
            ShowSecondHeadphoneOutputs(_lastAudioOutputs);
            DualHeadphonesStatus.Text = target is null ? "İkinci ses çıkışını seç." : "Ses çıkışları kontrol ediliyor…";
        }

        _dualHeadphones.SetTarget(target, (message, token) =>
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                if (!_exiting && !token.IsCancellationRequested)
                    DualHeadphonesStatus.Text = message;
            });
        });
    }
}
