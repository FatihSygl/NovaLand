using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace NovaIsland.Services;

public sealed record UpdateRelease(Version Version, Uri Download, long Size, string Sha256);

// Kept independent of WPF so hostile archives and release data can be tested.
public static class UpdatePackage
{
    public const long MaxDownload = 120L * 1024 * 1024;
    public const long MaxExpanded = 350L * 1024 * 1024;
    public const string Repository = "FatihSygl/NovaLand";

    public static UpdateRelease? ParseRelease(string json, Version current)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version) || version <= current) return null;
        // Releases have one predictable application asset; source archives are never installed.
        var name = $"NovaLand_v{version}_win-x64.zip";
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != name) continue;
            long size = asset.GetProperty("size").GetInt64();
            var hash = asset.TryGetProperty("digest", out var digest) ? digest.GetString() : null;
            if (size <= 0 || size > MaxDownload || hash is null || !hash.StartsWith("sha256:", StringComparison.Ordinal))
                throw new InvalidDataException("Paket doğrulama bilgisi eksik.");
            hash = hash[7..];
            if (hash.Length != 64 || !hash.All(Uri.IsHexDigit)) throw new InvalidDataException("Geçersiz paket özeti.");
            var url = new Uri(asset.GetProperty("browser_download_url").GetString()!);
            string expected = $"https://github.com/{Repository}/releases/download/{tag}/{name}";
            if (!string.Equals(url.AbsoluteUri, expected, StringComparison.Ordinal)) throw new InvalidDataException("Beklenmeyen paket adresi.");
            return new(version, url, size, hash);
        }
        return null;
    }

    public static async Task VerifyAsync(string path, UpdateRelease release, CancellationToken token)
    {
        if (new FileInfo(path).Length != release.Size) throw new InvalidDataException("Paket eksik indi.");
        await using var stream = File.OpenRead(path);
        var actual = await SHA256.HashDataAsync(stream, token);
        if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(release.Sha256)))
            throw new InvalidDataException("Paket doğrulanamadı.");
    }

    public static void Extract(string archive, string destination, Version version)
    {
        Directory.CreateDirectory(destination);
        var prefix = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count > 3000) throw new InvalidDataException("Paket çok büyük.");
        long total = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            total = checked(total + entry.Length);
            if (total > MaxExpanded) throw new InvalidDataException("Paket çok büyük.");
            var relative = entry.FullName.Replace('\\', '/');
            if (!relative.StartsWith("NovaLand/", StringComparison.Ordinal)) throw new InvalidDataException("Geçersiz paket yapısı.");
            relative = relative[9..];
            if (relative.Length == 0) continue;
            // Reject traversal, NTFS alternate streams, reserved devices and ambiguous Win32 names.
            foreach (var part in relative.TrimEnd('/').Split('/'))
            {
                var stem = part.Split('.')[0].ToUpperInvariant();
                if (part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
                    part.Any(c => c < 32 || "<>:\"|?*".Contains(c)) ||
                    stem is "CON" or "PRN" or "AUX" or "NUL" ||
                    (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '0' and <= '9'))
                    throw new InvalidDataException("Güvensiz dosya adı.");
            }
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("Bağlantılı dosyaya izin yok.");
            var target = Path.GetFullPath(Path.Combine(destination, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !seen.Add(target)) throw new InvalidDataException("Geçersiz dosya yolu.");
            if (relative.EndsWith('/')) Directory.CreateDirectory(target);
            else { Directory.CreateDirectory(Path.GetDirectoryName(target)!); entry.ExtractToFile(target, overwrite: false); }
        }
        foreach (var required in new[] { "NovaLand.exe", "NovaLand.dll", "NovaLand.deps.json", "NovaLand.runtimeconfig.json" })
            if (!File.Exists(Path.Combine(destination, required))) throw new InvalidDataException("Uygulama dosyaları eksik.");
        var identity = System.Reflection.AssemblyName.GetAssemblyName(Path.Combine(destination, "NovaLand.dll"));
        if (identity.Name != "NovaLand" || identity.Version != new Version(version.Major, version.Minor, Math.Max(0, version.Build), 0))
            throw new InvalidDataException("Paket sürümü uyuşmuyor.");
    }
}
