using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
namespace NovaIsland.Models;
public sealed class ImageEntry
{
    public bool IsPinned { get; internal set; }
    public string PinHint => IsPinned ? "Sabitlemeyi kaldır" : "Sabitle";
    public string PinColor => IsPinned ? "#FFB45B" : "#8E8E93";
    public byte[] Png { get; }
    public string Key { get; }
    public string Name { get; }
    public DateTime At { get; }
    public BitmapImage Thumbnail { get; }
    public string Detail => $"{At:HH:mm} · {Png.Length / 1024:N0} KB";
    public ImageEntry(byte[] png, string name, DateTime at)
    {
        Png = png; Key = Convert.ToHexString(SHA256.HashData(png)); Name = name; At = at;
        using var stream = new MemoryStream(png);
        Thumbnail = new BitmapImage(); Thumbnail.BeginInit(); Thumbnail.CacheOption = BitmapCacheOption.OnLoad;
        Thumbnail.DecodePixelWidth = 240; Thumbnail.StreamSource = stream; Thumbnail.EndInit(); Thumbnail.Freeze();
    }
}
public sealed class ImageHistory
{
    public ObservableCollection<ImageEntry> Items { get; } = new();
    public ObservableCollection<ImageEntry> Pinned { get; } = new();
    public void RestorePin(byte[] png, string name, DateTime at)
    {
        if (png.Length == 0 || png.Length > 32 * 1024 * 1024) return;
        var item = new ImageEntry(png, name, at) { IsPinned = true };
        if (!Pinned.Any(x => x.Key == item.Key)) Pinned.Add(item);
    }
    public void TogglePin(ImageEntry item)
    {
        if (item.IsPinned)
        {
            Pinned.Remove(item); item.IsPinned = false;
            var old = Items.FirstOrDefault(x => x.Key == item.Key); if (old is not null) Items.Remove(old);
            Items.Insert(0, item); while (Items.Count > 7) Items.RemoveAt(Items.Count - 1);
        }
        else { Items.Remove(item); item.IsPinned = true; Pinned.Add(item); }
    }
    public void Add(byte[] png, string name, DateTime at)
    {
        if (png.Length == 0 || png.Length > 32 * 1024 * 1024) return;
        var item = new ImageEntry(png, name, at);
        if (Pinned.Any(x => x.Key == item.Key)) return;
        var old = Items.FirstOrDefault(x => x.Key == item.Key);
        if (old is not null) { if (old.At >= at) return; Items.Remove(old); }
        int index = 0; while (index < Items.Count && Items[index].At > at) index++;
        Items.Insert(index, item); while (Items.Count > 7) Items.RemoveAt(Items.Count - 1);
    }
    public void Clear() => Items.Clear();
}
public sealed record NoticeEntry(string Title, string Body, DateTime At)
{
    public string Time => At.ToString("HH:mm");
}
