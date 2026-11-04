using System.IO;
using System.Text.Json;
namespace NovaIsland.Services;
public sealed record ManagedExtension(string Folder, string Version, string InstallId);
public static class ManagedBrowserExtension
{
    private const string Marker = ".novaland-managed";
    public static string DefaultFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaIsland", "BrowserExtension");
    private static readonly string[] Files = ["manifest.json","background.js","core.js","media.js","popup.html","popup.js","managed.js","icon16.png","icon32.png","icon48.png","icon128.png"];
    public static ManagedExtension Prepare(string source, string target, string key)
    {
        if (key.Length != 64 || !key.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid pairing key");
        source = Path.GetFullPath(source); target = Path.GetFullPath(target);
        if (source.Equals(target, StringComparison.OrdinalIgnoreCase)) throw new IOException("Package cannot be its own managed installation");
        if (Directory.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) throw new IOException("Managed folder cannot be a link");
        var marker = Path.Combine(target, Marker);
        string id;
        if (File.Exists(marker))
        {
            id = File.ReadAllText(marker).Trim();
            if (!Guid.TryParseExact(id, "N", out _)) throw new IOException("Invalid managed folder marker");
        }
        else
        {
            if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any()) throw new IOException("Refusing to replace an unrelated folder");
            id = Guid.NewGuid().ToString("N");
        }
        var bytes = Files.ToDictionary(name => name, name => File.ReadAllBytes(Path.Combine(source, name)));
        using var manifest = JsonDocument.Parse(bytes["manifest.json"]);
        var version = manifest.RootElement.GetProperty("version").GetString()!;
        if (!Version.TryParse(version, out var next)) throw new IOException("Invalid extension version");
        var currentManifest = Path.Combine(target, "manifest.json");
        if (File.Exists(currentManifest))
        {
            using var installed = JsonDocument.Parse(File.ReadAllBytes(currentManifest));
            if (Version.TryParse(installed.RootElement.GetProperty("version").GetString(), out var current) && current > next)
                return new(target, current.ToString(), id);
        }
        bytes["managed.js"] = System.Text.Encoding.UTF8.GetBytes("globalThis.NovaManagedConfig = " + JsonSerializer.Serialize(new {installId=id,key}) + ";\n");
        // Read the entire trusted package before changing the user's stable installation.
        Directory.CreateDirectory(target);
        var previous = bytes.Keys.Append(Marker).ToDictionary(name=>name, name=>File.Exists(Path.Combine(target,name)) ? File.ReadAllBytes(Path.Combine(target,name)) : null);
        try
        {
            foreach (var item in bytes.Where(item=>item.Key!="manifest.json"))
                Replace(Path.Combine(target,item.Key),item.Value);
            Replace(marker,System.Text.Encoding.UTF8.GetBytes(id));
            // Manifest is committed last; the worker reloads only after native startup finishes.
            Replace(currentManifest,bytes["manifest.json"]);
        }
        catch
        {
            foreach (var item in previous)
                try { if(item.Value is null) File.Delete(Path.Combine(target,item.Key)); else Replace(Path.Combine(target,item.Key),item.Value); } catch { }
            throw;
        }
        return new(target,version,id);
    }
    private static void Replace(string path, byte[] bytes)
    {
        if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) return;
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp,bytes); File.Move(temp,path,true); }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
}
