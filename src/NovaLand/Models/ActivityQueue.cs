namespace NovaIsland.Models;
public sealed class ActivityQueue<T> where T : class
{
    public sealed record Item(string Key, int Priority, T Value);
    private readonly List<Item> _pending = new();
    private DateTimeOffset _ends, _pausedAt;
    private bool _paused;
    public Item? Active { get; private set; }
    public bool HasWork => Active is not null || _pending.Count > 0;
    public event Action<T?>? Changed;
    public void Enqueue(string key, int priority, T value, DateTimeOffset now, bool replaceActive = false)
    {
        _pending.RemoveAll(x => x.Key == key);
        var item = new Item(key, priority, value);
        if (!_paused && replaceActive && Active?.Key == key && priority >= Active.Priority)
        { Active = item; _ends = now.AddSeconds(4); Changed?.Invoke(value); return; }
        if (!_paused && Active is not null && priority > Active.Priority)
        {
            // Resume the interrupted event after the urgent event, with a full viewing period.
            if (Active.Key != key && !_pending.Any(x => x.Key == Active.Key)) _pending.Insert(0, Active);
            Active = null;
            _pending.Insert(0, item); StartNext(now); return;
        }
        // Coalesce repeated songs/status updates while keeping only the newest queued version.
        _pending.Add(item);
        if (!_paused && Active is null) StartNext(now);
        if (_pending.Count > 24) _pending.RemoveAt(_pending.FindLastIndex(x => x.Priority == _pending.Min(y => y.Priority)));
    }
    public void Pause(bool paused, DateTimeOffset now)
    {
        if (_paused == paused) return;
        _paused = paused;
        if (paused) _pausedAt = now;
        else
        {
            if (Active is not null) _ends += now - _pausedAt;
            if (Active is null) StartNext(now);
            else if (_pending.Any(x => x.Priority > Active.Priority || x.Key == Active.Key && x.Priority >= Active.Priority))
            {
                var old = Active; Active = null; if (!_pending.Any(x => x.Key == old.Key)) _pending.Insert(0, old); StartNext(now);
            }
        }
    }
    public void Tick(DateTimeOffset now)
    {
        if (_paused) return;
        if (Active is not null && now >= _ends) { Active = null; StartNext(now); }
        else if (Active is null && _pending.Count > 0) StartNext(now);
    }
    private void StartNext(DateTimeOffset now)
    {
        if (_pending.Count == 0) { Changed?.Invoke(null); return; }
        int index = _pending.FindIndex(x => x.Priority == _pending.Max(y => y.Priority));
        Active = _pending[index]; _pending.RemoveAt(index);
        _ends = now.AddSeconds(4); Changed?.Invoke(Active.Value);
    }
    public void RemovePending(string key) => _pending.RemoveAll(x => x.Key == key);
    public void Clear() { _pending.Clear(); Active = null; Changed?.Invoke(null); }
}
