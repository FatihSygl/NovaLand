using System.IO;
using System.Text.Json;
using NovaIsland.Models;
namespace NovaIsland.Services;
public sealed class ReminderStore
{
    private readonly string _path;
    public ReminderStore(string? path = null) => _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaIsland", "reminders.json");
    public List<ReminderEntry> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new();
            return (JsonSerializer.Deserialize<List<ReminderEntry>>(File.ReadAllText(_path)) ?? new())
                .Where(x => x is not null && x.Id != Guid.Empty && !string.IsNullOrWhiteSpace(x.Text) && x.Text.Length <= 300).DistinctBy(x => x.Id).ToList();
        }
        catch { return new(); }
    }
    public void Save(IEnumerable<ReminderEntry> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(items.ToArray()));
        File.Move(temp, _path, true);
    }
}
