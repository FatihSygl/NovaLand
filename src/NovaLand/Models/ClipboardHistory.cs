using System.Collections.ObjectModel;
namespace NovaIsland.Models;
public sealed class ClipboardEntry(string text)
{
    public string Text { get; } = text;
    public bool IsPinned { get; internal set; }
    public string PinHint => IsPinned ? "Sabitlemeyi kaldır" : "Sabitle";
    public string PinColor => IsPinned ? "#FFB45B" : "#8E8E93";
    public DateTime CopiedAt { get; } = DateTime.Now;
    public string Preview { get { var s = Text.Replace("\r", "").Replace("\n", "  "); return s.Length > 150 ? s[..150] + "…" : s; } }
    public string Detail => $"{(Uri.TryCreate(Text.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http") ? "Bağlantı" : "Metin")} · {Text.Length:N0} karakter · {CopiedAt:HH:mm}";
}
public sealed class ClipboardHistory
{
    public ObservableCollection<ClipboardEntry> Items { get; } = new();
    public ObservableCollection<ClipboardEntry> Pinned { get; } = new();
    public void RestorePin(string text)
    {
        if (!string.IsNullOrWhiteSpace(text) && text.Length <= 1_000_000 && !Pinned.Any(x => x.Text == text))
            Pinned.Add(new ClipboardEntry(text) { IsPinned = true });
    }
    public void TogglePin(ClipboardEntry item)
    {
        if (item.IsPinned)
        {
            Pinned.Remove(item); item.IsPinned = false;
            var old = Items.FirstOrDefault(x => x.Text == item.Text); if (old is not null) Items.Remove(old);
            Items.Insert(0, item); while (Items.Count > 10) Items.RemoveAt(Items.Count - 1);
        }
        else { Items.Remove(item); item.IsPinned = true; Pinned.Add(item); }
    }
    public void Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 1_000_000) return;
        if (Pinned.Any(x => x.Text == text)) return;
        var previous = Items.FirstOrDefault(x => x.Text == text);
        if (previous is not null) Items.Remove(previous);
        Items.Insert(0, new ClipboardEntry(text));
        while (Items.Count > 10) Items.RemoveAt(Items.Count - 1);
    }
    public void Clear() => Items.Clear();
}
