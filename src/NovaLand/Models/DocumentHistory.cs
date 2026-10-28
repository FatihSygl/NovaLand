using System.Collections.ObjectModel;
using System.IO;
namespace NovaIsland.Models;
public sealed record DocumentEntry(string FullPath, DateTime CopiedAt)
{
    public string Name => Path.GetFileName(FullPath.TrimEnd(Path.DirectorySeparatorChar));
    public string Extension => Path.GetExtension(FullPath).TrimStart('.').ToUpperInvariant();
    public string Detail => $"{(Extension.Length == 0 ? "Dosya" : Extension)} · {CopiedAt:HH:mm}";
}
public sealed class DocumentHistory
{
    public ObservableCollection<DocumentEntry> Items { get; } = new();
    public void Add(IEnumerable<string> paths)
    {
        var batch = new List<string>();
        foreach (var raw in paths.Take(100))
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Length > 32767 || !Path.IsPathFullyQualified(raw)) continue;
            try
            {
                var path = Path.GetFullPath(raw);
                if (!batch.Contains(path, StringComparer.OrdinalIgnoreCase)) batch.Add(path);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
            if (batch.Count == 10) break;
        }
        foreach (var path in batch.AsEnumerable().Reverse())
        {
            var old = Items.FirstOrDefault(x => string.Equals(x.FullPath, path, StringComparison.OrdinalIgnoreCase));
            if (old is not null) Items.Remove(old);
            Items.Insert(0, new DocumentEntry(path, DateTime.Now));
        }
        while (Items.Count > 10) Items.RemoveAt(Items.Count - 1);
    }
    public void Clear() => Items.Clear();
}
