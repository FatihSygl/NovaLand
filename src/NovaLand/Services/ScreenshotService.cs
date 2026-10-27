using System.IO;
using System.Runtime.InteropServices;
namespace NovaIsland.Services;
public sealed class ScreenshotService
{
    private readonly Dictionary<string, DateTime> _seen = new(StringComparer.OrdinalIgnoreCase);
    public event Action<byte[], string, DateTime>? ImageFound;
    public async Task ScanAsync()
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (!string.IsNullOrEmpty(pictures)) folders.Add(Path.Combine(pictures, "Screenshots"));
        IntPtr ptr = IntPtr.Zero;
        try
        {
            var id = new Guid("B7BEDE81-DF94-4682-A7D8-57A52620B86F");
            if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out ptr) == 0)
            { var p = Marshal.PtrToStringUni(ptr); if (!string.IsNullOrEmpty(p)) folders.Add(p); }
        }
        catch { }
        finally { if (ptr != IntPtr.Zero) Marshal.FreeCoTaskMem(ptr); }
        foreach (var folder in folders)
        {
            try
            {
                var latest = await Task.Run(() => Directory.Exists(folder) ? new DirectoryInfo(folder).EnumerateFiles()
                    .Where(f => new[] { ".png", ".jpg", ".jpeg", ".bmp" }.Contains(f.Extension.ToLowerInvariant()))
                    .OrderByDescending(f => f.LastWriteTime).Take(7).Select(f => (f.FullName, f.Name, f.LastWriteTime)).ToArray() : []);
                foreach (var file in latest)
                {
                    if (_seen.TryGetValue(file.FullName, out var at) && at == file.LastWriteTime) continue;
                    try
                    {
                        var png = await Task.Run(() => ImageTools.ReadFile(file.FullName));
                        if (png is not null) ImageFound?.Invoke(png, file.Name, file.LastWriteTime);
                        _seen[file.FullName] = file.LastWriteTime;
                    }
                    catch (IOException) { } // A screenshot may still be being written: retry next scan.
                    catch (Exception) { _seen[file.FullName] = file.LastWriteTime; }
                }
                // Bounded tracking; five current files per screenshot folder suffice.
                var current = latest.Select(f => f.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var path in _seen.Keys.Where(p => string.Equals(Path.GetDirectoryName(p), folder, StringComparison.OrdinalIgnoreCase) && !current.Contains(p)).ToArray()) _seen.Remove(path);
            }
            catch { }
        }
    }
    [DllImport("shell32.dll")] private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);
}
