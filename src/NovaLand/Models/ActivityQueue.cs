namespace NovaIsland.Models;
public sealed class ActivityQueue<T> where T : class
{
    public sealed record Item(string Key, int Priority, T Value, bool RequiresAcknowledgment);
    private readonly List<Item> _pending = new();
    private DateTimeOffset _ends, _pausedAt;
    private bool _paused;
    public Item? Active { get; private set; }
    public bool AwaitingAcknowledgment => Active?.RequiresAcknowledgment == true;
    public bool HasWork => Active is not null || _pending.Count > 0;
    public event Action<T?>? Changed;
    public void Enqueue(string key, int priority, T value, DateTimeOffset now, bool replaceActive = false, bool requiresAcknowledgment = false)
    {
        _pending.RemoveAll(x => x.Key == key);
        var item = new Item(key, priority, value, requiresAcknowledgment);
        if (!_paused && !AwaitingAcknowledgment && replaceActive && Active?.Key == key && priority >= Active.Priority)
        { Active = item; _ends = now.AddSeconds(4); Changed?.Invoke(value); return; }
        if (!_paused && !AwaitingAcknowledgment && Active is not null && priority > Active.Priority)
        {
            // Resume the interrupted event after the urgent event, with a full viewing period.
            if (Active.Key != key && !_pending.Any(x => x.Key == Active.Key)) _pending.Insert(0, Active);
            Active = null;
            _pending.Insert(0, item); StartNext(now); return;
        }
        // Coalesce repeated songs/status updates while keeping only the newest queued version.
        _pending.Add(item);
        if (!_paused && Active is null) StartNext(now);
        if (_pending.Count > 24)
        {
            var discard = _pending.AsEnumerable().Reverse().Where(x => !x.RequiresAcknowledgment).MinBy(x => x.Priority);
            if (discard is not null) _pending.Remove(discard);
        }
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
            else if (!AwaitingAcknowledgment && _pending.Any(x => x.Priority > Active.Priority || x.Key == Active.Key && x.Priority >= Active.Priority))
            {
                var old = Active; Active = null; if (!_pending.Any(x => x.Key == old.Key)) _pending.Insert(0, old); StartNext(now);
            }
        }
    }
    public void Tick(DateTimeOffset now)
    {
        if (_paused) return;
        if (Active is not null && !AwaitingAcknowledgment && now >= _ends) { Active = null; StartNext(now); }
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
    public bool Acknowledge(DateTimeOffset now)
    {
        if (_paused || !AwaitingAcknowledgment) return false;
        Active = null;
        StartNext(now);
        return true;
    }

    public void ClearTransient(DateTimeOffset now)
    {
        _pending.RemoveAll(x => !x.RequiresAcknowledgment);
        if (AwaitingAcknowledgment) return;
        Active = null;
        if (_paused) Changed?.Invoke(null);
        else StartNext(now);
    }
}
