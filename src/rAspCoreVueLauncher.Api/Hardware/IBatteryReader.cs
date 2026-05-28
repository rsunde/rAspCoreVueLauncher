using rAspCoreVueLauncher.Shared.Hardware;

namespace rAspCoreVueLauncher.Api.Hardware;

public interface IBatteryReader
{
    Task<BatterySnapshot?> ReadAsync();
}
