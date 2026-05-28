using rAspCoreVueLauncher.Shared.Hardware;

namespace rAspCoreVueLauncher.Api.Hardware;

public interface IMobileSensorCache
{
    void Store(MobileSensorReading reading);
    IReadOnlyList<MobileSensorReading> GetAll();
}

public sealed class MobileSensorCache : IMobileSensorCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private readonly object _lock = new();
    private readonly Dictionary<string, (MobileSensorReading Reading, DateTime StoredAt)> _store = new();

    public void Store(MobileSensorReading reading)
    {
        lock (_lock)
        {
            _store[reading.ClientId] = (reading, DateTime.UtcNow);
            EvictStale();
        }
    }

    public IReadOnlyList<MobileSensorReading> GetAll()
    {
        lock (_lock)
        {
            EvictStale();
            return _store.Values.Select(v => v.Reading).ToList();
        }
    }

    private void EvictStale()
    {
        var cutoff = DateTime.UtcNow - Ttl;
        foreach (var key in _store.Where(kv => kv.Value.StoredAt < cutoff).Select(kv => kv.Key).ToList())
            _store.Remove(key);
    }
}
