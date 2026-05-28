using System.Runtime.InteropServices;
using rAspCoreVueLauncher.Shared.Hardware;

namespace rAspCoreVueLauncher.Api.Hardware;

public sealed class WindowsBatteryReader : IBatteryReader
{
    [DllImport("kernel32.dll", SetLastError = false)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus lpSystemPowerStatus);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;        // 0=offline, 1=online, 255=unknown
        public byte BatteryFlag;         // 128=no battery, 8=charging, 1/2/4=high/low/critical
        public byte BatteryLifePercent;  // 0-100; 255=unknown
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    public Task<BatterySnapshot?> ReadAsync()
    {
        if (!GetSystemPowerStatus(out var s))
            return Task.FromResult<BatterySnapshot?>(null);

        if ((s.BatteryFlag & 128) != 0 || s.BatteryLifePercent == 255)
            return Task.FromResult<BatterySnapshot?>(null);

        return Task.FromResult<BatterySnapshot?>(new BatterySnapshot(
            PercentRemaining: s.BatteryLifePercent,
            IsCharging: (s.BatteryFlag & 0x08) != 0,
            EstimatedRuntime: null));
    }
}
