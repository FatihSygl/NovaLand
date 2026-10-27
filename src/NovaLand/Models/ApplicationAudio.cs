using System.ComponentModel;

namespace NovaIsland.Models;

public interface IQuickAudioTarget
{
    string Key { get; }
    string Name { get; }
    string Kind { get; }
    string Detail { get; }
    int Volume { get; set; }
    string Percent { get; }
}
public sealed record AppAudioSession(string DeviceId, string InstanceId, uint ProcessId);
public sealed record AppAudioSnapshot(string Key, string Name, int Volume, bool Playing, AppAudioSession[] Sessions);

public sealed class ApplicationAudioRow : IQuickAudioTarget, INotifyPropertyChanged
{
    public string Key => Snapshot.Key;
    public string Name => Snapshot.Name;
    public string Kind => "Uygulama";
    public string Detail => Snapshot.Playing ? "Masaüstü uygulaması · Ses etkin" : "Masaüstü uygulaması · Ses oturumu açık";
    public AppAudioSnapshot Snapshot { get; private set; }
    private int _volume;
    private DateTimeOffset _pendingUntil;
    public string Percent => $"%{_volume}";
    public int Volume
    {
        get => _volume;
        set
        {
            int next = Math.Clamp(value, 0, 100);
            if (next == _volume) return;
            _volume = next; _pendingUntil = DateTimeOffset.UtcNow.AddSeconds(5);
            Changed(nameof(Volume)); Changed(nameof(Percent)); Requested?.Invoke(this, next);
        }
    }
    public event Action<ApplicationAudioRow, int>? Requested;
    public event PropertyChangedEventHandler? PropertyChanged;
    public ApplicationAudioRow(AppAudioSnapshot snapshot) { Snapshot = snapshot; _volume = snapshot.Volume; }
    public void Update(AppAudioSnapshot snapshot)
    {
        Snapshot = snapshot;
        if (DateTimeOffset.UtcNow >= _pendingUntil || snapshot.Volume == _volume)
        { _volume = snapshot.Volume; _pendingUntil = DateTimeOffset.MinValue; Changed(nameof(Volume)); Changed(nameof(Percent)); }
        Changed(nameof(Name)); Changed(nameof(Detail));
    }
    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
