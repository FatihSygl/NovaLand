using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace NovaIsland.Services;

public sealed class UpdateService : IDisposable
{
    private static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaIsland", "Updates");
    private static readonly string CurrentFile = Path.Combine(Root, "current.json");
    private readonly CancellationTokenSource _stop = new();
    private readonly HttpClient _http;
    public static Version CurrentVersion => new Version(Assembly.GetExecutingAssembly().GetName().Version!.ToString(3));
    public string? ReadyExecutable { get; private set; }
    public Version? ReadyVersion { get; private set; }

    public UpdateService() : this(new HttpClient { Timeout = TimeSpan.FromMinutes(4) }) { }

    internal UpdateService(HttpClient http)
    {
        _http = http;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("NovaLand/" + CurrentVersion);
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<bool> CheckAndDownloadAsync(Action<string> status)
    {
        if (ReadyExecutable is not null) return true;
        using var response = await _http.GetAsync($"https://api.github.com/repos/{UpdatePackage.Repository}/releases/latest", HttpCompletionOption.ResponseHeadersRead, _stop.Token);
        if (response.StatusCode == HttpStatusCode.NotFound) { status("Henüz yayımlanmış bir sürüm yok."); return false; }
        response.EnsureSuccessStatusCode();
        await using var metadata = await response.Content.ReadAsStreamAsync(_stop.Token);
        using var limited = new MemoryStream();
        await CopyLimitedAsync(metadata, limited, 2 * 1024 * 1024, _stop.Token);
        var release = UpdatePackage.ParseRelease(System.Text.Encoding.UTF8.GetString(limited.ToArray()), CurrentVersion);
        if (release is null) { status("Güncel sürümü kullanıyorsun."); return false; }
        status($"{release.Version} indiriliyor…");
        Directory.CreateDirectory(Root);
        var stage = Path.Combine(Root, "stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        bool ready = false;
        try
        {
            var archive = Path.Combine(stage, "update.zip");
            using var package = await _http.GetAsync(release.Download, HttpCompletionOption.ResponseHeadersRead, _stop.Token);
            package.EnsureSuccessStatusCode();
            await using (var input = await package.Content.ReadAsStreamAsync(_stop.Token))
            await using (var output = File.Create(archive))
                await CopyLimitedAsync(input, output, release.Size, _stop.Token);
            await UpdatePackage.VerifyAsync(archive, release, _stop.Token);
            status("Paket doğrulanıyor…");
            var app = Path.Combine(stage, "app");
            await Task.Run(() => UpdatePackage.Extract(archive, app, release.Version), _stop.Token);
            File.Delete(archive);
            ReadyExecutable = Path.Combine(app, "NovaLand.exe");
            ReadyVersion = release.Version;
            ready = true;
            status($"{release.Version} hazır. Yeniden başlatınca uygulanacak.");
            return true;
        }
        finally { if (!ready) { try { Directory.Delete(stage, true); } catch { } } }
    }

    private static async Task CopyLimitedAsync(Stream input, Stream output, long max, CancellationToken token)
    {
        byte[] buffer = new byte[65536]; long count = 0; int read;
        while ((read = await input.ReadAsync(buffer, token)) > 0)
        {
            count += read;
            if (count > max) throw new InvalidDataException("İndirme boyutu aşıldı.");
            await output.WriteAsync(buffer.AsMemory(0, read), token);
        }
    }

    public async Task StartReadyAsync()
    {
        if (ReadyExecutable is null || !File.Exists(ReadyExecutable)) throw new InvalidOperationException("Güncelleme hazır değil.");
        var info = new ProcessStartInfo(ReadyExecutable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(ReadyExecutable)! };
        info.ArgumentList.Add("--update-parent"); info.ArgumentList.Add(Environment.ProcessId.ToString());
        var nonce = Guid.NewGuid().ToString("N");
        info.ArgumentList.Add("--update-token"); info.ArgumentList.Add(nonce);
        var marker = Path.Combine(Path.GetDirectoryName(ReadyExecutable)!, "handoff-" + nonce + ".ready");
        using var child = Process.Start(info) ?? throw new IOException("Yeni sürüm başlatılamadı.");
        try
        {
            // Keep the old app alive if the new runtime cannot even start.
            for (int i = 0; i < 50; i++)
            {
                if (File.Exists(marker)) return;
                if (child.HasExited) break;
                await Task.Delay(100, _stop.Token);
            }
            throw new IOException("Yeni sürüm hazır olduğunu bildirmedi.");
        }
        finally { try { File.Delete(marker); } catch { } }
    }

    // The old portable shortcut follows the successfully started newer version.
    // --recovery always starts that local copy and skips redirection.
    public static bool RedirectOrWait(string[] args)
    {
        var parentIndex = Array.IndexOf(args, "--update-parent");
        if (parentIndex >= 0 && parentIndex + 1 < args.Length && int.TryParse(args[parentIndex + 1], out int pid))
        {
            var tokenIndex = Array.IndexOf(args, "--update-token");
            try
            {
                if (tokenIndex >= 0 && tokenIndex + 1 < args.Length && Guid.TryParseExact(args[tokenIndex + 1], "N", out _))
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "handoff-" + args[tokenIndex + 1] + ".ready"), "ready");
                using var parent = Process.GetProcessById(pid);
                if (!parent.WaitForExit(15000)) return true;
            }
            catch (ArgumentException) { }
            catch (Exception ex) { DiagnosticLog.Write("Update handoff", ex); return true; }
            return false;
        }
        if (args.Contains("--recovery")) return false;
        try
        {
            if (!File.Exists(CurrentFile)) return false;
            var exe = JsonSerializer.Deserialize<string>(File.ReadAllText(CurrentFile));
            if (exe is null || !IsManagedCopy(exe) || !File.Exists(exe)) return false;
            var actual = AssemblyName.GetAssemblyName(Path.Combine(Path.GetDirectoryName(exe)!, "NovaLand.dll"));
            if (actual.Name != "NovaLand" || actual.Version is null || actual.Version <= Assembly.GetExecutingAssembly().GetName().Version) return false;
            var info = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
            if (args.Contains("--startup")) info.ArgumentList.Add("--startup");
            using var child = Process.Start(info);
            return child is not null;
        }
        catch (Exception ex) { DiagnosticLog.Write("Update redirect", ex); return false; }
    }

    private static bool IsManagedCopy(string exe)
    {
        var relative = Path.GetRelativePath(Root, Path.GetFullPath(exe)).Replace('\\', '/').Split('/');
        return relative.Length == 3 && relative[0].StartsWith("stage-", StringComparison.Ordinal) &&
            Guid.TryParseExact(relative[0][6..], "N", out _) && relative[1] == "app" && relative[2] == "NovaLand.exe";
    }

    public static void MarkSuccessfulStart()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (exe is null || !IsManagedCopy(exe)) return;
            Directory.CreateDirectory(Root);
            var temp = CurrentFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(exe));
            File.Move(temp, CurrentFile, true);
        }
        catch (Exception ex) { DiagnosticLog.Write("Update commit", ex); }
    }

    public void Dispose() { _stop.Cancel(); _http.Dispose(); _stop.Dispose(); }
}
