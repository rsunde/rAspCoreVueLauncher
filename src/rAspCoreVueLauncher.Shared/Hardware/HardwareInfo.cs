namespace rAspCoreVueLauncher.Shared.Hardware;

public record HardwareInfo(
    string OsPlatform,
    string OsDescription,
    string OsArchitecture,
    string MachineName,
    int ProcessorCount,
    long TotalMemoryMb,
    string RuntimeVersion);
