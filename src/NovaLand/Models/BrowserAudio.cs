using System.ComponentModel;

namespace NovaIsland.Models;

public sealed record BrowserTabAudio(string Id, string Host, int Volume, bool Muted, bool Playing, bool Active, int Count);
public sealed record BrowserAudioSnapshot(string Client, BrowserTabAudio Tab);
public sealed record BrowserVolumeCommand(string Id, string Tab, int Volume);
public sealed record BrowserAudioSync(string Client, BrowserTabAudio[] Tabs, string[] Acks);

public sealed class BrowserAudioRow : INotifyPropertyChanged, IQuickAudioTarget
{
    public string Key => "site:" + Client + ":" + Id;
    public string Kind => "Site";
    public string Client { get; }
    public string Id { get; }
    public string Host { get; private set; } = "";
    public string Name => Host switch { "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com" => "YouTube", "kick.com" or "www.kick.com" => "Kick", "twitch.tv" or "www.twitch.tv" => "Twitch", _ => Host.StartsWith("www.") ? Host[4..] : Host };
    public string Detail { get; private set; } = "";
    private int _volume;
    private DateTimeOffset _pendingUntil;
    public int Volume
    {
        get => _volume;
        set
        {
            var next = Math.Clamp(value, 0, 100);
            if (_volume == next) return;
            _volume = next; _pendingUntil = DateTimeOffset.UtcNow.AddSeconds(5);
            Changed(nameof(Volume)); Changed(nameof(Percent));
            Requested?.Invoke(this, next);
        }
    }
    public string Percent => $"%{_volume}";
    public event Action<BrowserAudioRow, int>? Requested;
    public event PropertyChangedEventHandler? PropertyChanged;
    public BrowserAudioRow(BrowserAudioSnapshot snapshot) { Client = snapshot.Client; Id = snapshot.Tab.Id; Update(snapshot.Tab); }
    public void Update(BrowserTabAudio tab)
    {
        Host = tab.Host;
        Detail = (tab.Active ? "Açık sekme · " : "") + (tab.Playing ? "Oynatılıyor" : "Duraklatıldı") + (tab.Muted ? " · Sessiz" : "");
        var actual = tab.Muted ? 0 : tab.Volume;
        if (DateTimeOffset.UtcNow >= _pendingUntil || actual == _volume)
        { _volume = actual; _pendingUntil = DateTimeOffset.MinValue; Changed(nameof(Volume)); Changed(nameof(Percent)); }
        Changed(nameof(Name)); Changed(nameof(Detail));
    }
    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
