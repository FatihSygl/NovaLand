using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;
namespace NovaIsland.Models;
public sealed class ReminderEntry : INotifyPropertyChanged
{
    public Guid Id { get; }
    public string Text { get; }
    public DateTimeOffset Due { get; }
    private bool _triggered;
    public bool IsTriggered
    {
        get => _triggered;
        set { if (_triggered == value) return; _triggered = value; PropertyChanged?.Invoke(this, new(nameof(IsTriggered))); PropertyChanged?.Invoke(this, new(nameof(Description))); }
    }
    [JsonIgnore] public string Description => $"{Due.LocalDateTime:dd MMM · HH:mm}" + (IsTriggered ? " · Hatırlatıldı" : " · Bekliyor");
    public event PropertyChangedEventHandler? PropertyChanged;
    [JsonConstructor] public ReminderEntry(Guid id, string text, DateTimeOffset due, bool isTriggered) { Id = id; Text = text; Due = due; _triggered = isTriggered; }
}
public sealed class ReminderBoard
{
    public ObservableCollection<ReminderEntry> Items { get; } = new();
    public static bool TryPlan(string text, string clock, DateTimeOffset now, TimeZoneInfo zone, out ReminderEntry? entry, out string error)
    {
        entry = null; error = ""; text = text.Trim();
        if (text.Length is < 1 or > 300) { error = "1–300 karakterlik bir hatırlatma yaz."; return false; }
        if (!DateTime.TryParseExact(clock.Trim(), new[] { "H:mm", "HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        { error = "Saati 14:30 biçiminde yaz."; return false; }
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var due = DateTime.SpecifyKind(localNow.Date + time.TimeOfDay, DateTimeKind.Unspecified);
        if (due <= localNow.DateTime) due = due.AddDays(1);
        if (zone.IsInvalidTime(due)) { error = "Seçilen saat yaz saati geçişinde yok. Başka saat seç."; return false; }
        entry = new ReminderEntry(Guid.NewGuid(), text, new DateTimeOffset(due, zone.GetUtcOffset(due)), false);
        return true;
    }
    public List<ReminderEntry> TakeDue(DateTimeOffset now)
    {
        var due = Items.Where(x => !x.IsTriggered && x.Due <= now).OrderBy(x => x.Due).ToList();
        foreach (var item in due) item.IsTriggered = true;
        return due;
    }
}
public static class FocusDuration
{
    public static bool TryParse(string text, out int minutes) => int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out minutes) && minutes is >= 1 and <= 1440;
}
