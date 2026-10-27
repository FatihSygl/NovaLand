using NovaIsland.Models;
using NovaIsland.Services;

namespace NovaIsland;

public partial class MainWindow
{
    private readonly List<IQuickAudioTarget> _audioTargets = [];
    private readonly List<ApplicationAudioRow> _appRows = [];
    private readonly Dictionary<string, (AppAudioSnapshot Snapshot, int Volume)> _appVolumePending = [];
    private bool _appScanBusy, _appVolumeBusy;
    private DateTimeOffset _nextAppScan;
    private async Task RefreshApplicationAudioAsync()
    {
        if (_appScanBusy || _exiting || !_expanded || _tab != "Music" || DateTimeOffset.UtcNow < _nextAppScan) return;
        _nextAppScan = DateTimeOffset.UtcNow.AddSeconds(2);
        _appScanBusy = true;
        try
        {
            var snapshots = await Task.Run(ApplicationAudioService.Read);
            if (_exiting) return;
            _appRows.RemoveAll(r => !snapshots.Any(s => s.Key == r.Key));
            foreach (var snapshot in snapshots)
            {
                var row = _appRows.FirstOrDefault(r => r.Key == snapshot.Key);
                if (row is null)
                {
                    row = new(snapshot);
                    row.Requested += (item, volume) => { _appVolumePending[item.Key] = (item.Snapshot, volume); _ = FlushAppVolumeAsync(); };
                    _appRows.Add(row);
                }
                else row.Update(snapshot);
            }
            RefreshQuickSite();
        }
        catch (Exception ex) { DiagnosticLog.Write("AppAudioRead", ex); }
        finally { _appScanBusy = false; }
    }
    private async Task FlushAppVolumeAsync()
    {
        if (_appVolumeBusy || _exiting) return;
        _appVolumeBusy = true;
        try
        {
            while (!_exiting && _appVolumePending.Count > 0)
            {
                var request = _appVolumePending.First(); _appVolumePending.Remove(request.Key);
                bool changed = false;
                try { changed = await Task.Run(() => ApplicationAudioService.SetVolume(request.Value.Snapshot, request.Value.Volume)); }
                catch (Exception ex) { DiagnosticLog.Write("AppAudioSet", ex); }
                if (!changed && !_appVolumePending.ContainsKey(request.Key) && !_exiting)
                    StatusText.Text = "Uygulamanın ses oturumu kapandı. Yeniden ses oynatmayı dene.";
            }
        }
        finally { _appVolumeBusy = false; }
    }
}
