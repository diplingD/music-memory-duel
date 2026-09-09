namespace Server.Utils;

// Fixed-window call counter per key. Pure — utcNow is passed in, never read internally,
// so it's testable without any SignalR/real-clock dependency (same principle as Reducer).
public sealed class RateLimiter(int maxCalls, int windowMs)
{
    private readonly Dictionary<string, (int Count, DateTime StartedAt)> _entries = new();  // key is ConnectionId
    private readonly Lock _lock = new();

    public bool TryConsume(string key, DateTime utcNow)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out var entry) || utcNow - entry.StartedAt >= TimeSpan.FromMilliseconds(windowMs))
                entry = (0, utcNow);

            entry.Count++;
            _entries[key] = entry;

            return entry.Count <= maxCalls;
        }
    }

    public void Remove(string key)
    {
        lock (_lock)
        {
            _entries.Remove(key);
        }
    }
}
