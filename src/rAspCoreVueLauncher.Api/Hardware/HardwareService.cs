using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using rAspCoreVueLauncher.Shared.Hardware;

namespace rAspCoreVueLauncher.Api.Hardware;

public sealed class HardwareService : IHardwareService
{
    private readonly object _cpuLock = new();
    private DateTime _lastCpuSampleAt = DateTime.UtcNow;
    private TimeSpan _lastTotalCpu = Process.GetCurrentProcess().TotalProcessorTime;
    private readonly IMobileSensorCache _mobileCache;
    private readonly IBatteryReader _batteryReader;

    public HardwareService(IMobileSensorCache mobileCache, IBatteryReader batteryReader)
    {
        _mobileCache = mobileCache;
        _batteryReader = batteryReader;
    }

    public Task<HardwareInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        var totalMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        var info = new HardwareInfo(
            OsPlatform: GetPlatform(),
            OsDescription: RuntimeInformation.OSDescription,
            OsArchitecture: RuntimeInformation.OSArchitecture.ToString(),
            MachineName: Environment.MachineName,
            ProcessorCount: Environment.ProcessorCount,
            TotalMemoryMb: totalMemoryBytes / (1024 * 1024),
            RuntimeVersion: RuntimeInformation.FrameworkDescription);

        return Task.FromResult(info);
    }

    public async Task<HardwareSensors> GetSensorsAsync(CancellationToken cancellationToken = default)
    {
        var process = Process.GetCurrentProcess();
        var gcInfo = GC.GetGCMemoryInfo();

        return new HardwareSensors(
            ServerTimeUtc: DateTimeOffset.UtcNow,
            ProcessUptime: DateTime.UtcNow - process.StartTime.ToUniversalTime(),
            Cpu: new CpuSnapshot(Environment.ProcessorCount, SampleProcessCpuPercent(process)),
            Memory: new MemorySnapshot(
                ProcessWorkingSetMb: process.WorkingSet64 / (1024 * 1024),
                TotalAvailableMb: gcInfo.TotalAvailableMemoryBytes / (1024 * 1024)),
            Disks: ReadDisks(),
            Networks: ReadNetworks(),
            Battery: await _batteryReader.ReadAsync(),
            Mobile: _mobileCache.GetLatest());
    }

    private double SampleProcessCpuPercent(Process process)
    {
        lock (_cpuLock)
        {
            var now = DateTime.UtcNow;
            var totalCpu = process.TotalProcessorTime;

            var wallElapsed = (now - _lastCpuSampleAt).TotalMilliseconds;
            var cpuElapsed = (totalCpu - _lastTotalCpu).TotalMilliseconds;

            _lastCpuSampleAt = now;
            _lastTotalCpu = totalCpu;

            if (wallElapsed <= 0) return 0;
            var percent = cpuElapsed / (wallElapsed * Environment.ProcessorCount) * 100.0;
            return Math.Round(Math.Clamp(percent, 0, 100), 2);
        }
    }

    private static IReadOnlyList<DiskSnapshot> ReadDisks()
    {
        var result = new List<DiskSnapshot>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady) continue;
                if (d.DriveType is DriveType.Ram or DriveType.Unknown or DriveType.NoRootDirectory) continue;
                result.Add(new DiskSnapshot(
                    Name: d.Name,
                    DriveFormat: d.DriveFormat,
                    TotalMb: d.TotalSize / (1024 * 1024),
                    FreeMb: d.AvailableFreeSpace / (1024 * 1024)));
            }
            catch { /* some pseudo-fs entries on Linux throw; skip them */ }
        }
        return result;
    }

    private static IReadOnlyList<NetworkInterfaceSnapshot> ReadNetworks()
    {
        var result = new List<NetworkInterfaceSnapshot>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            var ips = ni.GetIPProperties().UnicastAddresses
                .Where(a => a.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                .Select(a => a.Address.ToString())
                .ToList();
            if (ips.Count == 0) continue;
            result.Add(new NetworkInterfaceSnapshot(
                Name: ni.Name,
                Description: ni.Description,
                Status: ni.OperationalStatus.ToString(),
                IsLoopback: ni.NetworkInterfaceType == NetworkInterfaceType.Loopback,
                IpAddresses: ips));
        }
        return result;
    }

    private static string GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "Linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macOS";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD)) return "FreeBSD";
        return "Unknown";
    }
}
