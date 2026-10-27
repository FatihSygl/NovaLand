using System.IO;
using System.Text.Json;
using NovaIsland.Models;
namespace NovaIsland.Services;
public sealed class PinnedClipboardStore
{
    private readonly string _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaIsland", "PinnedClipboard");
    private readonly SemaphoreSlim _writeLock = new(1);
    private sealed record SavedImage(string Key, string Name, DateTime At);
    private sealed record SavedPins(string[] Texts, SavedImage[] Images);
    public void Load(ClipboardHistory text, ImageHistory images)
    {
        try
        {
            var path = Path.Combine(_folder, "pins.json"); if (!File.Exists(path)) return;
            var pins = JsonSerializer.Deserialize<SavedPins>(File.ReadAllText(path)); if (pins is null) return;
            foreach (var value in pins.Texts ?? []) text.RestorePin(value);
            foreach (var image in pins.Images ?? [])
            {
                if (image.Key.Length != 64 || !image.Key.All(Uri.IsHexDigit)) continue;
                var file = Path.Combine(_folder, image.Key + ".png");
                try { if (new FileInfo(file).Length <= 32 * 1024 * 1024) images.RestorePin(File.ReadAllBytes(file), image.Name, image.At); } catch { }
            }
        }
        catch { }
    }
    public async Task SaveAsync(ClipboardHistory text, ImageHistory images)
    {
        var texts = text.Pinned.Select(x => x.Text).ToArray();
        var pictureData = images.Pinned.Select(x => (x.Key, x.Name, x.At, x.Png)).ToArray();
        await _writeLock.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                Directory.CreateDirectory(_folder);
                foreach (var image in pictureData)
                {
                    var file = Path.Combine(_folder, image.Key + ".png");
                    if (!File.Exists(file)) File.WriteAllBytes(file, image.Png);
                }
                var saved = new SavedPins(texts, pictureData.Select(x => new SavedImage(x.Key, x.Name, x.At)).ToArray());
                var temp = Path.Combine(_folder, "pins.tmp"); File.WriteAllText(temp, JsonSerializer.Serialize(saved));
                File.Move(temp, Path.Combine(_folder, "pins.json"), true);
                var keep = pictureData.Select(x => x.Key + ".png").ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var file in Directory.EnumerateFiles(_folder, "*.png")) if (!keep.Contains(Path.GetFileName(file))) File.Delete(file);
            });
        }
        finally { _writeLock.Release(); }
    }
}
