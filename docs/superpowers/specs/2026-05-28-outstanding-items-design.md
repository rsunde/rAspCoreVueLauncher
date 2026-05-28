# Design: Battery Telemetry + Multi-Device Sensor History

**Date:** 2026-05-28
**Items:** Battery telemetry (server-side), Multi-device sensor history
**Status:** Approved

---

## 1. Battery Telemetry (server-side)

### Goal
Implement `BatterySnapshot` for the server host process. Currently hardcoded `null` in `HardwareService.GetSensorsAsync()`. Needs real values on Windows (WMI) and Linux (`/sys`).

### Architecture

Introduce `IBatteryReader` with three implementations:

| Class | Platform | Mechanism |
|---|---|---|
| `WindowsBatteryReader` | Windows | P/Invoke `GetSystemPowerStatus` (kernel32, no extra packages) |
| `LinuxBatteryReader` | Linux | `/sys/class/power_supply/BAT*/capacity` + `status` |
| `NullBatteryReader` | fallback | always returns `null` |

Registration in `Program.cs`:
```csharp
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    builder.Services.AddSingleton<IBatteryReader, WindowsBatteryReader>();
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    builder.Services.AddSingleton<IBatteryReader, LinuxBatteryReader>();
else
    builder.Services.AddSingleton<IBatteryReader, NullBatteryReader>();
```

`HardwareService` receives `IBatteryReader` via constructor injection and calls `ReadAsync()` in `GetSensorsAsync()`.

### Return shape

Existing `BatterySnapshot(int PercentRemaining, bool IsCharging, TimeSpan? EstimatedRuntime)` — unchanged.
- `EstimatedRuntime` is always `null` (unreliable on WMI, not available from `/sys` simply).
- Returns `null` if no battery present (desktop, VM, no `/sys` entries found).

### Files changed
- `src/rAspCoreVueLauncher.Api/Hardware/IBatteryReader.cs` (new)
- `src/rAspCoreVueLauncher.Api/Hardware/WindowsBatteryReader.cs` (new)
- `src/rAspCoreVueLauncher.Api/Hardware/LinuxBatteryReader.cs` (new)
- `src/rAspCoreVueLauncher.Api/Hardware/NullBatteryReader.cs` (new)
- `src/rAspCoreVueLauncher.Api/Hardware/HardwareService.cs` (inject + call)
- `src/rAspCoreVueLauncher.Api/Program.cs` (register)

---

## 2. Multi-Device Sensor History

### Goal
Replace the single-field latest-wins cache with a per-client dictionary. `GET /api/hardware/sensors` returns readings from **all** active clients within the TTL window.

### Architecture

**`MobileSensorCache.cs`** — replace `MobileSensorReading? _latest` with:
```csharp
private readonly Dictionary<string, (MobileSensorReading Reading, DateTime StoredAt)> _store = new();
private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
```

- `Store(reading)`: upserts by `reading.ClientId`, sets `StoredAt = DateTime.UtcNow`, evicts stale entries.
- `GetAll()`: evicts stale entries, returns `IReadOnlyList<MobileSensorReading>` of all remaining.
- `GetLatest()` removed; callers updated.
- Thread safety via existing `lock`.

**`HardwareService.cs`** — `GetSensorsAsync()` calls `_mobileCache.GetAll()`, puts result in response.

**Response shape** — `SensorsResponse` gains:
```csharp
IReadOnlyList<MobileSensorReading> MobileDevices  // was: MobileSensorReading? MobileDevice
```

**TypeScript types** (`src/rAspCoreVueLauncher.Web/src/types/hardware.ts`):
```typescript
mobileDevices: MobileSensorReading[]  // was: mobileDevice: MobileSensorReading | null
```

### TTL
30 seconds. Bridge posts every 2 s — 15 consecutive misses trigger eviction.

### Files changed
- `src/rAspCoreVueLauncher.Api/Hardware/MobileSensorCache.cs`
- `src/rAspCoreVueLauncher.Api/Hardware/HardwareService.cs`
- `src/rAspCoreVueLauncher.Shared/Hardware/HardwareSensors.cs` (response DTO update)
- `src/rAspCoreVueLauncher.Web/src/types/hardware.ts` (TS type update)
- Any Vue components consuming `mobileDevice` → `mobileDevices`
- Existing MSTest tests updated for new shape

---

## Out of Scope

- Mobile battery wiring into `BatterySnapshot` (already flows via `userInterface` field — no change)
- iOS support (macOS host required)
- Production API URL for mobile — already handled by `VITE_API_BASE_URL` in `.env.production.example`
- Tauri auto-update, code signing — require external certs/accounts
