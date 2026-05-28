using rAspCoreVueLauncher.Shared.Hardware;

namespace rAspCoreVueLauncher.Api.Hardware;

public sealed class NullBatteryReader : IBatteryReader
{
    public Task<BatterySnapshot?> ReadAsync() => Task.FromResult<BatterySnapshot?>(null);
}
