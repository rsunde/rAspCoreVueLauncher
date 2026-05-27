namespace rAspCoreVueLauncher.Shared.Hardware;

public record HardwareSensors(
    DateTimeOffset ServerTimeUtc,
    TimeSpan ProcessUptime,
    CpuSnapshot Cpu,
    MemorySnapshot Memory,
    IReadOnlyList<DiskSnapshot> Disks,
    IReadOnlyList<NetworkInterfaceSnapshot> Networks,
    BatterySnapshot? Battery);

public record CpuSnapshot(
    int LogicalCores,
    double ProcessUsagePercent);

public record MemorySnapshot(
    long ProcessWorkingSetMb,
    long TotalAvailableMb);

public record DiskSnapshot(
    string Name,
    string DriveFormat,
    long TotalMb,
    long FreeMb);

public record NetworkInterfaceSnapshot(
    string Name,
    string Description,
    string Status,
    bool IsLoopback,
    IReadOnlyList<string> IpAddresses);

public record BatterySnapshot(
    int PercentRemaining,
    bool IsCharging,
    TimeSpan? EstimatedRuntime);
