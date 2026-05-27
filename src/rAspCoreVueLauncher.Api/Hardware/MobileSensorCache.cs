using rAspCoreVueLauncher.Shared.Hardware;

namespace rAspCoreVueLauncher.Api.Hardware;

public interface IMobileSensorCache
{
    void Store(MobileSensorReading reading);
    MobileSensorReading? GetLatest();
}

public sealed class MobileSensorCache : IMobileSensorCache
{
    private readonly object _lock = new();
    private MobileSensorReading? _latest;

    public void Store(MobileSensorReading reading)
    {
        lock (_lock)
        {
            _latest = reading;
        }
    }

    public MobileSensorReading? GetLatest()
    {
        lock (_lock)
        {
            return _latest;
        }
    }
}
