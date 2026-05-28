using rAspCoreVueLauncher.Shared.Hardware;

namespace rAspCoreVueLauncher.Api.Hardware;

public sealed class LinuxBatteryReader : IBatteryReader
{
    private const string PowerSupplyPath = "/sys/class/power_supply";

    public Task<BatterySnapshot?> ReadAsync()
    {
        if (!Directory.Exists(PowerSupplyPath))
            return Task.FromResult<BatterySnapshot?>(null);

        foreach (var dir in Directory.EnumerateDirectories(PowerSupplyPath))
        {
            var typePath = Path.Combine(dir, "type");
            if (!File.Exists(typePath)) continue;
            if (!File.ReadAllText(typePath).Trim().Equals("Battery", StringComparison.OrdinalIgnoreCase)) continue;

            var capacityPath = Path.Combine(dir, "capacity");
            if (!File.Exists(capacityPath)) continue;
            if (!int.TryParse(File.ReadAllText(capacityPath).Trim(), out var percent)) continue;

            var statusPath = Path.Combine(dir, "status");
            var status = File.Exists(statusPath) ? File.ReadAllText(statusPath).Trim() : string.Empty;
            var isCharging = status.Equals("Charging", StringComparison.OrdinalIgnoreCase)
                          || status.Equals("Full", StringComparison.OrdinalIgnoreCase);

            return Task.FromResult<BatterySnapshot?>(new BatterySnapshot(
                PercentRemaining: Math.Clamp(percent, 0, 100),
                IsCharging: isCharging,
                EstimatedRuntime: null));
        }

        return Task.FromResult<BatterySnapshot?>(null);
    }
}
