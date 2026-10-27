using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
namespace NovaIsland.Models;
public sealed class TodoEntry : INotifyPropertyChanged
{
    public Guid Id { get; }
    public string Text { get; }
    private bool _isCompleted;
    public bool IsCompleted
    {
        get => _isCompleted;
        set { if (_isCompleted == value) return; _isCompleted = value; PropertyChanged?.Invoke(this, new(nameof(IsCompleted))); PropertyChanged?.Invoke(this, new(nameof(ToggleHint))); }
    }
    [JsonIgnore] public string ToggleHint => IsCompleted ? "Yeniden aç" : "Tamamla";
    public event PropertyChangedEventHandler? PropertyChanged;
    [JsonConstructor] public TodoEntry(Guid id, string text, bool isCompleted) { Id = id; Text = text; _isCompleted = isCompleted; }
}
public sealed class TodoBoard
{
    public ObservableCollection<TodoEntry> Items { get; } = new();
    private List<(int Index, TodoEntry Item)> _removed = new();
    public bool CanUndo => _removed.Count > 0;
    public bool Add(string text)
    {
        text = text.Trim(); if (text.Length == 0 || text.Length > 500) return false;
        Items.Insert(0, new TodoEntry(Guid.NewGuid(), text, false)); return true;
    }
    public void Remove(TodoEntry item)
    {
        int index = Items.IndexOf(item); if (index < 0) return;
        _removed = new() { (index, item) }; Items.RemoveAt(index);
    }
    public void Clear(bool onlyCompleted)
    {
        var selected = Items.Select((item, index) => (Index: index, Item: item)).Where(x => !onlyCompleted || x.Item.IsCompleted).ToList();
        if (selected.Count == 0) return;
        _removed = selected;
        foreach (var row in selected) Items.Remove(row.Item);
    }
    public void Undo()
    {
        foreach (var row in _removed.OrderBy(x => x.Index))
            if (!Items.Any(x => x.Id == row.Item.Id)) Items.Insert(Math.Min(row.Index, Items.Count), row.Item);
        _removed.Clear();
    }
}
