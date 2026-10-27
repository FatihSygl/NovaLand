using System.Windows;
using System.Windows.Media.Animation;
using NovaIsland.Models;
using NovaIsland.Services;

namespace NovaIsland;

public partial class MainWindow
{
    private bool _outputBusy;
    private ConnectedAudioRow[] _shownConnected = [];
    private OutputAudioRow[] _shownOutputs = [];
    private sealed record ConnectedAudioRow(string Name, int? Battery)
    {
        public string BatteryLabel => Battery.HasValue ? $"%{Math.Clamp(Battery.Value, 0, 100)}" : "—";
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
        var rows = _bluetooth.Connected.Select(x => new ConnectedAudioRow(x.Name, x.Battery)).ToArray();
        if (!_shownConnected.SequenceEqual(rows)) ConnectedAudioList.ItemsSource = _shownConnected = rows;
        ConnectedAudioEmpty.Visibility = _bluetooth.Connected.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
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
    private void ShowOutputAudio(AudioOutput[] rows)
    {
        var items = rows.Select(x => new OutputAudioRow(x.Id, x.Name, x.Selected)).ToArray();
        if (!_shownOutputs.SequenceEqual(items)) OutputAudioList.ItemsSource = _shownOutputs = items;
        OutputAudioEmpty.Text = "Ses çıkışı bulunamadı.";
        OutputAudioEmpty.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
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
            if (volume.HasValue) { _ready = false; VolumeSlider.Value = volume.Value; _ready = true; VolumeValue.Text = $"{volume.Value}%"; }
        }
        catch (Exception ex) { DiagnosticLog.Write("OutputChange", ex); StatusText.Text = "Ses çıkışı okunamadı. Windows ses ayarlarını kullanabilirsin."; }
        finally { _outputBusy = false; OutputAudioList.IsEnabled = true; }
    }
}
