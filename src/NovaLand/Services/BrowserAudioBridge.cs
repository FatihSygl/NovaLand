using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NovaIsland.Models;

namespace NovaIsland.Services;

// Only extension-origin HTTP on loopback, authenticated with a per-install key.
// No visited paths, titles, page contents or media URLs enter the native app.
public sealed class BrowserAudioBridge : IDisposable
{
    private sealed record ClientState(BrowserTabAudio[] Tabs, DateTimeOffset At, bool? SocialSitesBlocked);
    private sealed record Pending(string Client, BrowserVolumeCommand Command, DateTimeOffset At);
    private readonly object _gate = new();
    private readonly Dictionary<string, ClientState> _clients = new();
    private readonly Dictionary<string, Pending> _commands = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _connections = new(4);
    private readonly int _port;
    private TcpListener? _listener;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    private bool _blockSocialSites;
    public bool BlockSocialSites
    {
        get { lock (_gate) return _blockSocialSites; }
        set { lock (_gate) _blockSocialSites = value; }
    }
    public (bool Supported, bool Applied) SocialBlockingStatus
    {
        get
        {
            lock (_gate)
            {
                var clients = _clients.Values.Where(c => Fresh(c.At)).ToArray();
                return (clients.Any(c => c.SocialSitesBlocked.HasValue),
                    clients.Length > 0 && clients.All(c => c.SocialSitesBlocked == _blockSocialSites));
            }
        }
    }
    public string Key { get; }
    public bool Listening { get; private set; }
    public bool Connected { get { lock (_gate) return _clients.Values.Any(c => Fresh(c.At)); } }
    private static bool Fresh(DateTimeOffset at) => DateTimeOffset.UtcNow - at < TimeSpan.FromSeconds(12);

    public BrowserAudioBridge(string? key = null, int port = 47864)
    {
        _port = port; Key = key ?? LoadKey();
    }
    private static string LoadKey()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaIsland");
        Directory.CreateDirectory(folder); var path = Path.Combine(folder, "browser-audio.key");
        if (File.Exists(path))
        { var old = File.ReadAllText(path).Trim(); if (old.Length == 64 && old.All(Uri.IsHexDigit)) return old; }
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        File.WriteAllText(path, key); return key;
    }
    public bool Start()
    {
        try { _listener = new TcpListener(IPAddress.Loopback, _port); _listener.Start(8); Listening = true; _ = AcceptAsync(); return true; }
        catch (Exception ex) { DiagnosticLog.Write("Browser audio start", ex); return false; }
    }
    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener!.AcceptTcpClientAsync(_stop.Token);
                if (!await _connections.WaitAsync(0)) { client.Dispose(); continue; }
                _ = ServeAsync(client);
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex) { DiagnosticLog.Write("Browser audio accept", ex); }
    }
    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(3)); var ct = timeout.Token;
            try
            {
                var stream = client.GetStream(); using var data = new MemoryStream();
                var chunk = new byte[2048]; int end = -1;
                while (end < 0)
                {
                    int n = await stream.ReadAsync(chunk, ct); if (n == 0) return;
                    data.Write(chunk, 0, n);
                    var bytes = data.GetBuffer();
                    for (int i = Math.Max(0, (int)data.Length - n - 3); i <= data.Length - 4; i++)
                        if (bytes[i] == 13 && bytes[i+1] == 10 && bytes[i+2] == 13 && bytes[i+3] == 10) { end = i + 4; break; }
                    if ((end < 0 && data.Length > 8192) || end > 8192) return;
                }
                var lines = Encoding.ASCII.GetString(data.GetBuffer(), 0, end).Split("\r\n");
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in lines.Skip(1))
                { int colon = line.IndexOf(':'); if (colon > 0) headers[line[..colon].Trim()] = line[(colon+1)..].Trim(); }
                var origin = headers.GetValueOrDefault("Origin", "");
                if (origin.Length > 0 && !origin.StartsWith("chrome-extension://", StringComparison.Ordinal) && !origin.StartsWith("moz-extension://", StringComparison.Ordinal))
                { await Reply(stream, 403, "{}", "", ct); return; }
                if (lines[0] == "OPTIONS /sync HTTP/1.1") { await Reply(stream, 204, "", origin, ct); return; }
                if (lines[0] != "POST /sync HTTP/1.1") { await Reply(stream, 404, "{}", origin, ct); return; }
                var auth = Encoding.UTF8.GetBytes(headers.GetValueOrDefault("Authorization", ""));
                if (!CryptographicOperations.FixedTimeEquals(auth, Encoding.UTF8.GetBytes("Bearer " + Key)))
                { await Reply(stream, 401, "{}", origin, ct); return; }
                if (!int.TryParse(headers.GetValueOrDefault("Content-Length"), out int length) || length < 2 || length > 65536 || headers.ContainsKey("Transfer-Encoding"))
                { await Reply(stream, 400, "{}", origin, ct); return; }
                while (data.Length < end + length)
                { int n = await stream.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, end + length - (int)data.Length)), ct); if (n == 0) return; data.Write(chunk, 0, n); }
                var sync = JsonSerializer.Deserialize<BrowserAudioSync>(data.GetBuffer().AsSpan(end, length), Json);
                if (!Valid(sync)) { await Reply(stream, 400, "{}", origin, ct); return; }
                BrowserVolumeCommand[] commands;
                lock (_gate)
                {
                    foreach (var id in _clients.Where(x => !Fresh(x.Value.At)).Select(x => x.Key).ToArray()) _clients.Remove(id);
                    _clients[sync!.Client] = new(sync.Tabs, DateTimeOffset.UtcNow, sync.SocialSitesBlocked);
                    foreach (var entry in _commands.ToArray())
                        if (!Fresh(entry.Value.At) || entry.Value.Client == sync.Client && (sync.Acks.Contains(entry.Key) || !sync.Tabs.Any(t => t.Id == entry.Value.Command.Tab))) _commands.Remove(entry.Key);
                    commands = _commands.Values.Where(p => p.Client == sync.Client && DateTimeOffset.UtcNow - p.At < TimeSpan.FromSeconds(4)).Select(p => p.Command).ToArray();
                }
                await Reply(stream, 200, JsonSerializer.Serialize(new { commands, blockSocialSites = BlockSocialSites }, Json), origin, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is IOException or SocketException or JsonException or ArgumentException) { }
            catch (Exception ex) { DiagnosticLog.Write("Browser audio request", ex); }
            finally { _connections.Release(); }
        }
    }
    private static bool Valid(BrowserAudioSync? s) => s is not null && Guid.TryParse(s.Client, out _) && s.Tabs is not null && s.Acks is not null
        && s.Tabs.Length <= 64 && s.Acks.Length <= 128 && s.Tabs.All(t => t is not null && Guid.TryParse(t.Id, out _) && t.Host is not null
            && t.Host.Length is > 0 and <= 253 && Uri.CheckHostName(t.Host) == UriHostNameType.Dns && t.Volume is >= 0 and <= 100 && t.Count is > 0 and <= 256)
        && s.Tabs.Select(t => t.Id).Distinct().Count() == s.Tabs.Length;
    private static async Task Reply(NetworkStream stream, int status, string body, string origin, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var cors = origin.Length == 0 ? "" : $"Access-Control-Allow-Origin: {origin}\r\nVary: Origin\r\n";
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Response\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n{cors}Access-Control-Allow-Methods: POST, OPTIONS\r\nAccess-Control-Allow-Headers: Authorization, Content-Type\r\n\r\n");
        await stream.WriteAsync(header, ct); await stream.WriteAsync(bytes, ct);
    }
    public BrowserAudioSnapshot[] Snapshot()
    {
        lock (_gate) return _clients.Where(c => Fresh(c.Value.At)).SelectMany(c => c.Value.Tabs.Select(t => new BrowserAudioSnapshot(c.Key, t)))
            .OrderByDescending(s => s.Tab.Active).ThenByDescending(s => s.Tab.Playing).ToArray();
    }
    public bool SetVolume(string client, string tab, int volume)
    {
        lock (_gate)
        {
            if (!_clients.TryGetValue(client, out var state) || !Fresh(state.At) || !state.Tabs.Any(t => t.Id == tab)) return false;
            foreach (var id in _commands.Where(c => c.Value.Client == client && c.Value.Command.Tab == tab).Select(c => c.Key).ToArray()) _commands.Remove(id);
            var command = new BrowserVolumeCommand(Guid.NewGuid().ToString(), tab, Math.Clamp(volume, 0, 100));
            _commands[command.Id] = new(client, command, DateTimeOffset.UtcNow); return true;
        }
    }
    public void Dispose() { Listening = false; _stop.Cancel(); _listener?.Stop(); }
}
