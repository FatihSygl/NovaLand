using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace NovaIsland.Services;

public sealed class MediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private string _artKey = "";
    private ImageSource? _artwork;

    public async Task<MediaState> ReadAsync()
    {
        try
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _session = _manager.GetCurrentSession();
            if (_session is null)
            {
                _artKey = ""; _artwork = null;
                return new("Henüz bir şey çalmıyor", "Spotify veya YouTube’da bir şey aç", false, false, null, TimeSpan.Zero, TimeSpan.Zero, false);
            }
            var props = await _session.TryGetMediaPropertiesAsync();
            var info = _session.GetPlaybackInfo();
            var artKey = $"{_session.SourceAppUserModelId}|{props.Title}|{props.Artist}|{props.AlbumTitle}";
            if (artKey != _artKey || (_artwork is null && props.Thumbnail is not null))
            {
                _artKey = artKey;
                _artwork = null;
                try
                {
                    if (props.Thumbnail is not null)
                    {
                        using var stream = await props.Thumbnail.OpenReadAsync();
                        if (stream.Size > 0 && stream.Size <= 8 * 1024 * 1024)
                        {
                            using var reader = new DataReader(stream.GetInputStreamAt(0));
                            uint size = (uint)stream.Size;
                            uint loaded = await reader.LoadAsync(size);
                            var bytes = new byte[loaded];
                            reader.ReadBytes(bytes);
                            using var memory = new MemoryStream(bytes);
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            image.DecodePixelWidth = 160;
                            image.StreamSource = memory;
                            image.EndInit();
                            image.Freeze();
                            _artwork = image;
                        }
                    }
                }
                catch { /* Artwork is optional; transport remains usable. */ }
            }
            var timeline = _session.GetTimelineProperties();
            var start = timeline.StartTime;
            var duration = timeline.EndTime - start;
            var position = timeline.Position - start;
            if (info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                var elapsed = DateTimeOffset.UtcNow - timeline.LastUpdatedTime;
                if (elapsed >= TimeSpan.Zero && elapsed < TimeSpan.FromDays(1)) position += elapsed;
            }
            position = TimeSpan.FromSeconds(Math.Clamp(position.TotalSeconds, 0, Math.Max(0, duration.TotalSeconds)));
            return new(
                string.IsNullOrWhiteSpace(props.Title) ? "Bilinmeyen parça" : props.Title,
                string.IsNullOrWhiteSpace(props.Artist) ? _session.SourceAppUserModelId : props.Artist,
                info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                true, _artwork, position, duration, info?.Controls.IsPlaybackPositionEnabled == true);
        }
        catch
        {
            _manager = null; _session = null; _artwork = null; _artKey = "";
            return new("Medya oturumu kullanılamıyor", "Windows medya erişimini kontrol et", false, false, null, TimeSpan.Zero, TimeSpan.Zero, false);
        }
    }

    public async Task SeekAsync(TimeSpan position)
    {
        try
        {
            if (_session is null) return;
            var t = _session.GetTimelineProperties();
            var ticks = Math.Clamp((t.StartTime + position).Ticks, t.StartTime.Ticks, Math.Max(t.StartTime.Ticks, t.EndTime.Ticks));
            await _session.TryChangePlaybackPositionAsync(ticks);
        }
        catch { }
    }
    public async Task PlayPauseAsync()
    {
        try
        {
            if (_session is null) return;
            var playing = _session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            if (playing) await _session.TryPauseAsync(); else await _session.TryPlayAsync();
        }
        catch { }
    }
    public async Task PreviousAsync() { try { if (_session is not null) await _session.TrySkipPreviousAsync(); } catch { } }
    public async Task NextAsync() { try { if (_session is not null) await _session.TrySkipNextAsync(); } catch { } }
}

public sealed record MediaState(string Title, string Artist, bool IsPlaying, bool Available, ImageSource? Artwork, TimeSpan Position, TimeSpan Duration, bool CanSeek);
