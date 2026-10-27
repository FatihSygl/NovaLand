using System.IO;
using System.Text.Json;
using NovaIsland.Models;
namespace NovaIsland.Services;
public sealed class TodoStore
{
    private readonly string _path;
    public TodoStore(string? path = null) => _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaIsland", "todos.json");
    public List<TodoEntry> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new();
            return (JsonSerializer.Deserialize<List<TodoEntry>>(File.ReadAllText(_path)) ?? new())
                .Where(x => x is not null && x.Id != Guid.Empty && !string.IsNullOrWhiteSpace(x.Text) && x.Text.Length <= 500).DistinctBy(x => x.Id).ToList();
        }
        catch { return new(); }
    }
    public void Save(IEnumerable<TodoEntry> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(items.ToArray()));
        File.Move(temp, _path, true);
    }
}
