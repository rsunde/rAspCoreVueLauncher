namespace rAspCoreVueLauncher.Shared.Hardware;

public interface IHardwareService
{
    Task<HardwareInfo> GetInfoAsync(CancellationToken cancellationToken = default);
    Task<HardwareSensors> GetSensorsAsync(CancellationToken cancellationToken = default);
}
