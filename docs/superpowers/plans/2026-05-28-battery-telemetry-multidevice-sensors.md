# Battery Telemetry + Multi-Device Sensor History Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement real server-side battery readings via `IBatteryReader` (Windows P/Invoke + Linux `/sys`), and upgrade `MobileSensorCache` to track all active clients with 30-second TTL returning all devices from `GET /api/hardware/sensors`.

**Architecture:** Battery reading is extracted behind `IBatteryReader` (Api project) registered per-OS in `Program.cs`, injected into `HardwareService`. The mobile cache replaces `_latest` with `Dictionary<string, (Reading, StoredAt)>` with lazy TTL eviction; `HardwareSensors.Mobile` is renamed to `MobileDevices: IReadOnlyList<MobileSensorReading>` with matching TypeScript/Vue updates.

**Tech Stack:** ASP.NET Core 10, C# 13, MSTest, NSubstitute, P/Invoke (kernel32), Vue 3 + TypeScript

---

## File Map

**New files:**
- `src/rAspCoreVueLauncher.Api/Hardware/IBatteryReader.cs`
- `src/rAspCoreVueLauncher.Api/Hardware/NullBatteryReader.cs`
- `src/rAspCoreVueLauncher.Api/Hardware/WindowsBatteryReader.cs`
- `src/rAspCoreVueLauncher.Api/Hardware/LinuxBatteryReader.cs`

**Modified files:**
- `src/rAspCoreVueLauncher.Api/Hardware/HardwareService.cs` — inject `IBatteryReader`, make `GetSensorsAsync` async, call `GetAll()`
- `src/rAspCoreVueLauncher.Api/Hardware/MobileSensorCache.cs` — replace `_latest` with `Dictionary`; `GetLatest()` → `GetAll()`
- `src/rAspCoreVueLauncher.Api/Program.cs` — OS-aware `IBatteryReader` registration
- `src/rAspCoreVueLauncher.Shared/Hardware/HardwareSensors.cs` — `Mobile: MobileSensorReading?` → `MobileDevices: IReadOnlyList<MobileSensorReading>`
- `src/rAspCoreVueLauncher.Web/src/types/hardware.ts` — `mobile` → `mobileDevices`
- `src/rAspCoreVueLauncher.Web/src/components/SensorsPanel.vue` — iterate `mobileDevices` array
- `tests/rAspCoreVueLauncher.Api.Tests/Infrastructure/TestAppFactory.cs` — add `BatteryReaderSubstitute`
- `tests/rAspCoreVueLauncher.Api.Tests/HardwareEndpointTests.cs` — add battery tests, update mobile tests

---

## Task 1: IBatteryReader interface, NullBatteryReader, TestAppFactory, HardwareService wiring

**Files:**
- Create: `src/rAspCoreVueLauncher.Api/Hardware/IBatteryReader.cs`
- Create: `src/rAspCoreVueLauncher.Api/Hardware/NullBatteryReader.cs`
- Modify: `tests/rAspCoreVueLauncher.Api.Tests/Infrastructure/TestAppFactory.cs`
- Modify: `src/rAspCoreVueLauncher.Api/Program.cs`
- Modify: `src/rAspCoreVueLauncher.Api/Hardware/HardwareService.cs`
- Test: `tests/rAspCoreVueLauncher.Api.Tests/HardwareEndpointTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `tests/rAspCoreVueLauncher.Api.Tests/HardwareEndpointTests.cs` (add using at top: `using rAspCoreVueLauncher.Api.Hardware;`):

```csharp
[TestMethod]
public async Task GetSensors_Battery_WhenReaderReturnsBattery_IncludedInResponse()
{
    var fake = Substitute.For<IBatteryReader>();
    fake.ReadAsync().Returns(new BatterySnapshot(72, false, null));

    await using var factory = new TestAppFactory { BatteryReaderSubstitute = fake };
    var client = factory.CreateClient();

    var sensors = await client.GetFromJsonAsync<HardwareSensors>("/api/hardware/sensors");
    sensors!.Battery.Should().NotBeNull();
    sensors.Battery!.PercentRemaining.Should().Be(72);
    sensors.Battery.IsCharging.Should().BeFalse();
}
```

- [ ] **Step 2: Run test to verify it fails**

```
cd tests/rAspCoreVueLauncher.Api.Tests
dotnet test --filter "GetSensors_Battery_WhenReaderReturnsBattery_IncludedInResponse" -v normal
```

Expected: compile error — `IBatteryReader` does not exist yet.

- [ ] **Step 3: Create IBatteryReader**

Create `src/rAspCoreVueLauncher.Api/Hardware/IBatteryReader.cs`:

```csharp
using rAspCoreVueLauncher.Shared.Hardware;

namespace rAspCoreVueLauncher.Api.Hardware;

public interface IBatteryReader
{
    Task<BatterySnapshot?> ReadAsync();
}
```

- [ ] **Step 4: Create NullBatteryReader**

Create `src/rAspCoreVueLauncher.Api/Hardware/NullBatteryReader.cs`:

```csharp
using rAspCoreVueLauncher.Shared.Hardware;

namespace rAspCoreVueLauncher.Api.Hardware;

public sealed class NullBatteryReader : IBatteryReader
{
    public Task<BatterySnapshot?> ReadAsync() => Task.FromResult<BatterySnapshot?>(null);
}
```

- [ ] **Step 5: Update TestAppFactory to support BatteryReaderSubstitute**

Replace `tests/rAspCoreVueLauncher.Api.Tests/Infrastructure/TestAppFactory.cs` entirely:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using rAspCoreVueLauncher.Api.Data;
using rAspCoreVueLauncher.Api.Hardware;
using rAspCoreVueLauncher.Shared.Hardware;

namespace rAspCoreVueLauncher.Api.Tests.Infrastructure;

public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    public IHardwareService? HardwareSubstitute { get; set; }
    public IBatteryReader? BatteryReaderSubstitute { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor is not null) services.Remove(dbDescriptor);
            services.AddDbContext<AppDbContext>(o =>
                o.UseSqlite($"Data Source=file:test-{Guid.NewGuid():N}?mode=memory&cache=shared"));

            if (HardwareSubstitute is not null)
            {
                var hwDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IHardwareService));
                if (hwDescriptor is not null) services.Remove(hwDescriptor);
                services.AddSingleton(HardwareSubstitute);
            }

            if (BatteryReaderSubstitute is not null)
            {
                var batDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IBatteryReader));
                if (batDescriptor is not null) services.Remove(batDescriptor);
                services.AddSingleton(BatteryReaderSubstitute);
            }
        });
    }
}
```

- [ ] **Step 6: Register NullBatteryReader in Program.cs**

In `src/rAspCoreVueLauncher.Api/Program.cs`, add after `builder.Services.AddSingleton<IMobileSensorCache, MobileSensorCache>();`:

```csharp
builder.Services.AddSingleton<IBatteryReader, NullBatteryReader>();
```

Also add at top: `using rAspCoreVueLauncher.Api.Hardware;` (already present) and ensure `System.Runtime.InteropServices` is included — it's already used in `HardwareService.cs` but not in `Program.cs` yet; it will be needed in Task 3.

- [ ] **Step 7: Inject IBatteryReader into HardwareService**

Replace `src/rAspCoreVueLauncher.Api/Hardware/HardwareService.cs` entirely:

```csharp
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
            catch { }
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
```

Note: `Mobile: _mobileCache.GetLatest()` stays temporarily — it will be replaced in Task 5.

- [ ] **Step 8: Run all tests**

```
cd tests/rAspCoreVueLauncher.Api.Tests
dotnet test -v normal
```

Expected: all existing tests pass + new battery test passes.

- [ ] **Step 9: Commit**

```
git add src/rAspCoreVueLauncher.Api/Hardware/IBatteryReader.cs
git add src/rAspCoreVueLauncher.Api/Hardware/NullBatteryReader.cs
git add src/rAspCoreVueLauncher.Api/Hardware/HardwareService.cs
git add src/rAspCoreVueLauncher.Api/Program.cs
git add tests/rAspCoreVueLauncher.Api.Tests/Infrastructure/TestAppFactory.cs
git add tests/rAspCoreVueLauncher.Api.Tests/HardwareEndpointTests.cs
git commit -m "feat: add IBatteryReader interface + NullBatteryReader + HardwareService wiring"
```

---

## Task 2: WindowsBatteryReader (P/Invoke GetSystemPowerStatus)

**Files:**
- Create: `src/rAspCoreVueLauncher.Api/Hardware/WindowsBatteryReader.cs`
- Test: `tests/rAspCoreVueLauncher.Api.Tests/HardwareEndpointTests.cs`

- [ ] **Step 1: Write the test**

Add to `HardwareEndpointTests.cs`:

```csharp
[TestMethod]
public async Task GetSensors_Battery_NullBatteryReader_ReturnsNullBattery()
{
    // NullBatteryReader is registered by default in tests — battery should be null
    await using var factory = new TestAppFactory();
    var client = factory.CreateClient();

    var sensors = await client.GetFromJsonAsync<HardwareSensors>("/api/hardware/sensors");
    sensors!.Battery.Should().BeNull();
}
```

- [ ] **Step 2: Run test to verify it passes**

```
cd tests/rAspCoreVueLauncher.Api.Tests
dotnet test --filter "GetSensors_Battery_NullBatteryReader_ReturnsNullBattery" -v normal
```

Expected: PASS (NullBatteryReader is registered by default, returns null).

- [ ] **Step 3: Create WindowsBatteryReader**

Create `src/rAspCoreVueLauncher.Api/Hardware/WindowsBatteryReader.cs`:

```csharp
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
            IsCharging: s.ACLineStatus == 1,
            EstimatedRuntime: null));
    }
}
```

- [ ] **Step 4: Run all tests**

```
cd tests/rAspCoreVueLauncher.Api.Tests
dotnet test -v normal
```

Expected: all tests pass (WindowsBatteryReader not wired yet — tests still use NullBatteryReader).

- [ ] **Step 5: Commit**

```
git add src/rAspCoreVueLauncher.Api/Hardware/WindowsBatteryReader.cs
git add tests/rAspCoreVueLauncher.Api.Tests/HardwareEndpointTests.cs
git commit -m "feat: add WindowsBatteryReader using P/Invoke GetSystemPowerStatus"
```

---

## Task 3: LinuxBatteryReader + OS-aware Program.cs registration

**Files:**
- Create: `src/rAspCoreVueLauncher.Api/Hardware/LinuxBatteryReader.cs`
- Modify: `src/rAspCoreVueLauncher.Api/Program.cs`

- [ ] **Step 1: Create LinuxBatteryReader**

Create `src/rAspCoreVueLauncher.Api/Hardware/LinuxBatteryReader.cs`:

```csharp
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
```

- [ ] **Step 2: Update Program.cs to use OS-aware registration**

In `src/rAspCoreVueLauncher.Api/Program.cs`, replace:

```csharp
builder.Services.AddSingleton<IBatteryReader, NullBatteryReader>();
```

with:

```csharp
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    builder.Services.AddSingleton<IBatteryReader, WindowsBatteryReader>();
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    builder.Services.AddSingleton<IBatteryReader, LinuxBatteryReader>();
else
    builder.Services.AddSingleton<IBatteryReader, NullBatteryReader>();
```

Also add at the top of `Program.cs` if not already present:
```csharp
using System.Runtime.InteropServices;
```

- [ ] **Step 3: Run all tests**

```
cd tests/rAspCoreVueLauncher.Api.Tests
dotnet test -v normal
```

Expected: all tests pass. Tests still use `NullBatteryReader` (substituted via `TestAppFactory`) or mock.

- [ ] **Step 4: Commit**

```
git add src/rAspCoreVueLauncher.Api/Hardware/LinuxBatteryReader.cs
git add src/rAspCoreVueLauncher.Api/Program.cs
git commit -m "feat: add LinuxBatteryReader + OS-aware IBatteryReader registration"
```

---

## Task 4: MobileSensorCache — multi-device Dictionary with 30-second TTL

**Files:**
- Modify: `src/rAspCoreVueLauncher.Api/Hardware/MobileSensorCache.cs`
- Test: `tests/rAspCoreVueLauncher.Api.Tests/HardwareEndpointTests.cs`

**NOTE:** `HardwareSensors.Mobile` is still `MobileSensorReading?` at this point. This task only changes the cache internals and interface. `HardwareService` calling `GetLatest()` will stop compiling after this task — that's fixed in Task 5. Do Tasks 4 and 5 in the same session.

- [ ] **Step 1: Write the failing multi-device test**

Replace the existing `PostMobileSensors_LatestReadingWinsOverOlder` test in `HardwareEndpointTests.cs` with:

```csharp
[TestMethod]
public async Task PostMobileSensors_MultipleDevices_AllReturnedInGetSensors()
{
    await using var factory = new TestAppFactory();
    var client = factory.CreateClient();

    var phoneA = new MobileSensorReading(
        ClientId: "phone-a",
        CapturedAtUtc: DateTimeOffset.UtcNow,
        Device: null,
        Motion: new MotionSensors(
            Accelerometer: new Vector3(1.0, 1.0, 1.0),
            Gyroscope: null, Magnetometer: null, Gravity: null,
            LinearAcceleration: null, RotationVector: null,
            UserAcceleration: null, StepCount: null, Cadence: null),
        Orientation: null, Environment: null, Location: null,
        Health: null, Biometric: null, Connectivity: null, UserInterface: null);

    var phoneB = new MobileSensorReading(
        ClientId: "phone-b",
        CapturedAtUtc: DateTimeOffset.UtcNow,
        Device: null,
        Motion: new MotionSensors(
            Accelerometer: new Vector3(2.0, 2.0, 2.0),
            Gyroscope: null, Magnetometer: null, Gravity: null,
            LinearAcceleration: null, RotationVector: null,
            UserAcceleration: null, StepCount: null, Cadence: null),
        Orientation: null, Environment: null, Location: null,
        Health: null, Biometric: null, Connectivity: null, UserInterface: null);

    (await client.PostAsJsonAsync("/api/hardware/sensors/mobile", phoneA)).StatusCode
        .Should().Be(HttpStatusCode.Accepted);
    (await client.PostAsJsonAsync("/api/hardware/sensors/mobile", phoneB)).StatusCode
        .Should().Be(HttpStatusCode.Accepted);

    var sensors = await client.GetFromJsonAsync<HardwareSensors>("/api/hardware/sensors");
    sensors!.MobileDevices.Should().HaveCount(2);
    sensors.MobileDevices.Should().Contain(d => d.ClientId == "phone-a");
    sensors.MobileDevices.Should().Contain(d => d.ClientId == "phone-b");
}
```

- [ ] **Step 2: Run test to verify it fails**

```
cd tests/rAspCoreVueLauncher.Api.Tests
dotnet test --filter "PostMobileSensors_MultipleDevices_AllReturnedInGetSensors" -v normal
```

Expected: compile error — `MobileDevices` does not exist on `HardwareSensors` yet (it's still `Mobile`). This is expected — proceed to Task 5 immediately.

---

## Task 5: HardwareSensors DTO rename + HardwareService + cache implementation + all tests

**Files:**
- Modify: `src/rAspCoreVueLauncher.Shared/Hardware/HardwareSensors.cs`
- Modify: `src/rAspCoreVueLauncher.Api/Hardware/MobileSensorCache.cs`
- Modify: `src/rAspCoreVueLauncher.Api/Hardware/HardwareService.cs`
- Modify: `tests/rAspCoreVueLauncher.Api.Tests/HardwareEndpointTests.cs`

- [ ] **Step 1: Update HardwareSensors record**

In `src/rAspCoreVueLauncher.Shared/Hardware/HardwareSensors.cs`, replace the `HardwareSensors` record:

```csharp
public record HardwareSensors(
    DateTimeOffset ServerTimeUtc,
    TimeSpan ProcessUptime,
    CpuSnapshot Cpu,
    MemorySnapshot Memory,
    IReadOnlyList<DiskSnapshot> Disks,
    IReadOnlyList<NetworkInterfaceSnapshot> Networks,
    BatterySnapshot? Battery,
    IReadOnlyList<MobileSensorReading> MobileDevices);
```

(All other records in the file remain unchanged.)

- [ ] **Step 2: Update MobileSensorCache**

Replace `src/rAspCoreVueLauncher.Api/Hardware/MobileSensorCache.cs` entirely:

```csharp
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
```

- [ ] **Step 3: Update HardwareService to use GetAll() and MobileDevices**

In `src/rAspCoreVueLauncher.Api/Hardware/HardwareService.cs`, in `GetSensorsAsync`, replace:

```csharp
            Mobile: _mobileCache.GetLatest());
```

with:

```csharp
            MobileDevices: _mobileCache.GetAll());
```

- [ ] **Step 4: Update existing mobile tests to use MobileDevices**

In `tests/rAspCoreVueLauncher.Api.Tests/HardwareEndpointTests.cs`:

Replace `PostMobileSensors_StoresReading_AndIsReturnedByGetSensors`:

```csharp
[TestMethod]
public async Task PostMobileSensors_StoresReading_AndIsReturnedByGetSensors()
{
    await using var factory = new TestAppFactory();
    var client = factory.CreateClient();

    var reading = new MobileSensorReading(
        ClientId: "test-phone",
        CapturedAtUtc: DateTimeOffset.UtcNow,
        Device: null,
        Motion: new MotionSensors(
            Accelerometer: new Vector3(0.1, 0.2, 9.8),
            Gyroscope: null, Magnetometer: null, Gravity: null,
            LinearAcceleration: null, RotationVector: null,
            UserAcceleration: null, StepCount: null, Cadence: null),
        Orientation: null, Environment: null, Location: null,
        Health: null, Biometric: null, Connectivity: null, UserInterface: null);

    var post = await client.PostAsJsonAsync("/api/hardware/sensors/mobile", reading);
    post.StatusCode.Should().Be(HttpStatusCode.Accepted);

    var sensors = await client.GetFromJsonAsync<HardwareSensors>("/api/hardware/sensors");
    sensors!.MobileDevices.Should().HaveCount(1);
    var device = sensors.MobileDevices[0];
    device.ClientId.Should().Be("test-phone");
    device.Motion!.Accelerometer.Should().Be(new Vector3(0.1, 0.2, 9.8));
}
```

Also update `GetSensors_ReturnsLivePayload` — `sensors.Mobile` was never asserted in that test, so no change needed there.

- [ ] **Step 5: Run all tests**

```
cd tests/rAspCoreVueLauncher.Api.Tests
dotnet test -v normal
```

Expected: all tests pass including the new multi-device test.

- [ ] **Step 6: Commit**

```
git add src/rAspCoreVueLauncher.Shared/Hardware/HardwareSensors.cs
git add src/rAspCoreVueLauncher.Api/Hardware/MobileSensorCache.cs
git add src/rAspCoreVueLauncher.Api/Hardware/HardwareService.cs
git add tests/rAspCoreVueLauncher.Api.Tests/HardwareEndpointTests.cs
git commit -m "feat: multi-device sensor cache with 30s TTL; HardwareSensors.MobileDevices"
```

---

## Task 6: TypeScript types + Vue component update

**Files:**
- Modify: `src/rAspCoreVueLauncher.Web/src/types/hardware.ts`
- Modify: `src/rAspCoreVueLauncher.Web/src/components/SensorsPanel.vue`

- [ ] **Step 1: Update hardware.ts**

In `src/rAspCoreVueLauncher.Web/src/types/hardware.ts`, replace the `HardwareSensors` interface:

```typescript
export interface HardwareSensors {
  serverTimeUtc: string
  processUptime: string
  cpu: CpuSnapshot
  memory: MemorySnapshot
  disks: DiskSnapshot[]
  networks: NetworkInterfaceSnapshot[]
  battery: BatterySnapshot | null
  mobileDevices: MobileSensorReading[]
}
```

- [ ] **Step 2: Update SensorsPanel.vue**

In `src/rAspCoreVueLauncher.Web/src/components/SensorsPanel.vue`, replace the `mobileBlocks` computed and the mobile section in the template.

Replace the `mobileBlocks` computed (lines 70-84):

```typescript
const mobileBlocks = computed(() => {
  return (props.sensors?.mobileDevices ?? []).map(m => ({
    clientId: m.clientId,
    capturedAtUtc: m.capturedAtUtc,
    blocks: [
      { title: 'Device', data: m.device },
      { title: 'Motion', data: m.motion },
      { title: 'Orientation', data: m.orientation },
      { title: 'Environment', data: m.environment },
      { title: 'Location', data: m.location },
      { title: 'Health', data: m.health },
      { title: 'Biometric', data: m.biometric },
      { title: 'Connectivity', data: m.connectivity },
      { title: 'UserInterface', data: m.userInterface },
    ].filter(b => b.data && entries(b.data as Record<string, unknown>).length > 0),
  }))
})
```

Replace the mobile sensors section in the template (lines 171-189):

```vue
<!-- Mobile sensors -->
<div v-for="device in mobileBlocks" :key="device.clientId"
  class="rounded-lg border bg-card p-4 text-card-foreground shadow-sm">
  <div class="mb-3 flex items-baseline justify-between gap-2">
    <h3 class="text-sm font-medium">Mobile sensors</h3>
    <span class="font-mono text-xs text-muted-foreground tabular-nums">
      {{ device.clientId }} · {{ fmtRelative(device.capturedAtUtc) }}
    </span>
  </div>
  <div class="grid gap-3 sm:grid-cols-2">
    <div v-for="b in device.blocks" :key="b.title"
      class="rounded-lg border bg-card p-3 text-card-foreground shadow-sm">
      <h4 class="mb-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">{{ b.title }}</h4>
      <dl class="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-xs">
        <template v-for="[k, v] in entries(b.data as Record<string, unknown>)" :key="k">
          <dt class="text-muted-foreground">{{ humanLabel(k) }}</dt>
          <dd class="font-mono tabular-nums text-right">{{ fmtVal(v) }}</dd>
        </template>
      </dl>
    </div>
  </div>
</div>
```

- [ ] **Step 3: TypeScript check**

```
cd src/rAspCoreVueLauncher.Web
npx vue-tsc --noEmit
```

Expected: no errors.

- [ ] **Step 4: Commit**

```
git add src/rAspCoreVueLauncher.Web/src/types/hardware.ts
git add src/rAspCoreVueLauncher.Web/src/components/SensorsPanel.vue
git commit -m "feat: update frontend for mobileDevices array + multi-device sensor display"
```

---

## Self-Review Checklist (already applied)

- **Spec coverage:** Battery (IBatteryReader + Windows + Linux + null) ✓; Multi-device cache (Dictionary + TTL + GetAll) ✓; DTO rename (Mobile → MobileDevices) ✓; TypeScript + Vue update ✓
- **Placeholders:** None — all steps contain complete code
- **Type consistency:** `IMobileSensorCache.GetAll()` returns `IReadOnlyList<MobileSensorReading>` defined in Task 5 step 2, used in Task 5 step 3. `IBatteryReader.ReadAsync()` defined in Task 1 step 3, used everywhere. `MobileDevices` property used in tests (Task 4/5) defined in Task 5 step 1.
- **One note:** Tasks 4 and 5 must be done in the same session — the test written in Task 4 step 1 won't compile until Task 5 step 1 adds `MobileDevices`. This is by design (TDD across a breaking change).
