# Hardware Diagnostic Test Pages — Design

**Date:** 2026-05-29
**Status:** Approved design (pre-implementation)

## Goal

Add diagnostic "test pages" to the launcher's Vue frontend that surface **everything**
the hardware layer exposes, for verifying the hardware/sensor pipeline end-to-end.
The pages are verification-oriented (raw, exhaustive) rather than a polished dashboard:
every field is labeled with units, values update live, each page shows fetch status
(timestamp, HTTP status, age) and a raw-JSON toggle, and empty/error states are explicit.

## Scope

- **In scope:** A `/hardware` hub plus one diagnostic page per data group — static info,
  CPU, memory, disks, networks, battery, and mobile/device sensors. The mobile page covers
  every cached device's nine sensor categories AND a "this client" panel showing what the
  local browser sensor bridge is currently capturing.
- **Out of scope (YAGNI):** charts/history/sparklines (current values only), alerting,
  data export, editing/writing any hardware state, and any new backend endpoints (the pages
  consume the existing `/api/hardware/info` and `/api/hardware/sensors`).

## Approach

The launcher already has a `useHardwareStore` (Pinia) that loads `/api/hardware/info` once
and polls `/api/hardware/sensors` (default 2 s). All seven live categories
(cpu, memory, disks, networks, battery, mobileDevices) are fields of the **single**
`/sensors` response; only `info` is separate.

**Chosen approach (A):** extend `useHardwareStore` with per-fetch *metadata* and let each
page read its slice of the shared state. One poller for `/sensors`, one load for `/info`,
no duplicate fetching. (Rejected: a separate diagnostics store — duplicates polling and
re-fetches `/sensors`; per-page composables — each category page would re-fetch the same
`/sensors` payload N times.)

Because the SPA renders one route at a time, the diagnostic pages and `HomeView` are never
mounted simultaneously, so sharing the store's polling lifecycle is safe. `startPolling` is
already idempotent (guards a null handle) and `stopPolling` clears it.

## Architecture

```
Web/src/
  router/index.ts                 + nested /hardware routes
  App.vue                         + "Hardware" RouterLink → /hardware
  stores/hardware.ts              + infoMeta / sensorsMeta fetch metadata
  lib/sensorsBridge.ts            + peekLatestLocalReading() module accessor
  main.ts                         register bridge handle for the accessor
  components/diagnostics/
    HardwareTestPage.vue          shared diagnostic chrome (header + slot)
    FieldRow.vue                  label → value [unit], "—" for null/undefined
  views/hardware/
    HardwareHubView.vue           /hardware
    HardwareInfoView.vue          /hardware/info
    HardwareCpuView.vue           /hardware/cpu
    HardwareMemoryView.vue        /hardware/memory
    HardwareDisksView.vue         /hardware/disks
    HardwareNetworksView.vue      /hardware/networks
    HardwareBatteryView.vue       /hardware/battery
    HardwareMobileView.vue        /hardware/mobile
```

Data flow: each view → `useHardwareStore()` → existing axios calls → `/api/hardware/*`.
The mobile view additionally reads `peekLatestLocalReading()` from `sensorsBridge.ts` for
its "this client" panel.

## Routes & pages

A "Hardware" link is added to the `App.vue` nav, pointing at the hub. Routes are registered
in `router/index.ts` (lazy-loaded, matching the existing `/about` pattern).

| Route | Source | Contents |
|---|---|---|
| `/hardware` | both | Hub: a link to each page + a one-line live status/value per category (e.g. CPU 3.4 %, 2 disks, battery 87 %), and the shared fetch-status line. |
| `/hardware/info` | `GET /info` | The 7 static `HardwareInfo` fields. **Static** — load once with a Refresh button; no polling. |
| `/hardware/cpu` | `/sensors`.cpu | `logicalCores`, `processUsagePercent` (%). |
| `/hardware/memory` | `/sensors`.memory | `processWorkingSetMb` (MB), `totalAvailableMb` (MB). |
| `/hardware/disks` | `/sensors`.disks[] | One block per disk: `name`, `driveFormat`, `totalMb`, `freeMb` (MB). Empty state: "no drives reported". |
| `/hardware/networks` | `/sensors`.networks[] | One block per NIC: `name`, `description`, `status`, `isLoopback`, `ipAddresses[]`. Empty state: "no interfaces". |
| `/hardware/battery` | `/sensors`.battery | `percentRemaining` (%), `isCharging`, `estimatedRuntime`. Null state: "no battery present". |
| `/hardware/mobile` | `/sensors`.mobileDevices[] **+ local bridge** | See below. |

### `/hardware/mobile` page

Two regions:

1. **Cached devices** — from `/sensors`.mobileDevices[]. For each `MobileSensorReading`,
   render `clientId` + `capturedAtUtc` and every populated category. All nine optional
   categories are shown with each field labeled; a field that is null/undefined renders `—`,
   and an absent category renders a muted "not reported". Categories and fields:
   - `device` — manufacturer, model, osName, osVersion, locale, timeZone, isPhysicalDevice
   - `motion` — accelerometer (Vec3), gyroscope (Vec3), magnetometer (Vec3), gravity (Vec3),
     linearAcceleration (Vec3), rotationVector (Vec4), userAcceleration (Vec3), stepCount, cadence
   - `orientation` — pitch, roll, yaw, compassHeading, trueHeading, headingAccuracyDegrees, screenOrientation
   - `environment` — ambientLightLux, proximityCm, isNear, ambientTemperatureCelsius,
     relativeHumidityPercent, pressureHpa, altitudeMeters, uvIndex
   - `location` — latitude, longitude, altitudeMeters, accuracyMeters, altitudeAccuracyMeters,
     headingDegrees, speedMetersPerSecond, provider, isMocked, satelliteCount, fixTimestampUtc
   - `health` — heartRateBpm, heartRateVariabilityMs, bloodOxygenPercent, respiratoryRateBpm,
     bodyTemperatureCelsius, skinTemperatureCelsius, stepsToday, distanceMetersToday,
     activeEnergyKcalToday, vO2MaxMlPerKgPerMin, sleepStage, stressLevel
   - `biometric` — fingerprintAvailable, faceUnlockAvailable, irisAvailable, voiceUnlockAvailable,
     strongBiometricEnrolled, authenticationStatus
   - `connectivity` — networkType, carrierName, signalStrengthDbm, wifiRssiDbm, wifiSsid,
     isMetered, isRoaming, airplaneMode, bluetoothEnabled, nfcAvailable, nfcEnabled
   - `userInterface` — screenBrightness, keyguardLocked, appState, hapticsAvailable, flashlightOn,
     ambientNoiseDb, headphonesPluggedIn, isMuted

   Empty state when the array is empty: "no devices reporting".

   Vector values (Vec3/Vec4) render compactly, e.g. `x 0.01  y -0.20  z 9.81`.

2. **This client (local sensor bridge)** — reads `peekLatestLocalReading()` from
   `sensorsBridge.ts`, polled ~1 s. Shows the bridge's most recent locally-captured reading
   (same field layout as a cached device) plus a status line: capturing / awaiting permission /
   unsupported / not started. This distinguishes "the local bridge is capturing" from "the
   server cached/echoed it back", testing the full client→server→client loop.

## Shared components

### `HardwareTestPage.vue`
A wrapper providing the diagnostic chrome so every category page is consistent. Props:
`title: string`, `endpoint: string` (display string, e.g. `/api/hardware/sensors`),
`meta` (the relevant fetch-metadata object, optional for static pages), `live: boolean`
(whether this page polls). It renders a header:

- title + endpoint label
- a live indicator: ●live (green) when polling and `meta.ok`, ⏸ paused when not polling,
  ✕ error (red) when `meta.ok === false`
- "fetched N ms/s ago" derived from `meta.fetchedAt`, and the HTTP `status`
- buttons: **Refresh now**, **Pause/Resume** (hidden on static pages), and a **raw JSON**
  toggle that reveals a `<pre>` of the slice (`JSON.stringify(slice, null, 2)`)
- a default `<slot>` for the field rows
- loading and error banners (error shows message + HTTP status prominently)

It emits `refresh`, `toggle-pause`; the parent view wires those to the store
(`loadSensors()` / `stopPolling()` / `startPolling()`) and passes the slice for raw JSON
via a `rawValue` prop or a named slot.

### `FieldRow.vue`
Props `label: string`, `value: unknown`, `unit?: string`. Renders a label→value row in a
mono/tabular style. Null/undefined/empty-string render as a muted `—`. Booleans render
`true`/`false`. This is the atomic unit every category page composes.

## Store changes (`useHardwareStore`, additive)

Add two reactive metadata objects and populate them inside the existing actions:

```ts
interface FetchMeta {
  fetchedAt: number | null   // performance.now() / Date.now() at completion
  durationMs: number | null
  httpStatus: number | null
  ok: boolean                // true on 2xx, false on error
}
const infoMeta = ref<FetchMeta>({ fetchedAt: null, durationMs: null, httpStatus: null, ok: false })
const sensorsMeta = ref<FetchMeta>({ fetchedAt: null, durationMs: null, httpStatus: null, ok: false })
```

`loadInfo`/`loadSensors` time the call (`performance.now()` before/after), set `httpStatus`
from the axios response (or the error's `response?.status`), set `ok`, and stamp `fetchedAt`
(`Date.now()`). Both metas are returned from the store. No change to existing state, polling,
or `HomeView` behavior — purely additive.

(Raw-JSON display needs no store change; pages stringify their own slice.)

## Sensor-bridge accessor (`sensorsBridge.ts`, additive)

Today `startSensorBridge()` returns a handle (with `latest()`) that `main.ts` discards.
Add a module-level singleton so components can read the latest local reading without holding
the handle:

```ts
// module scope
let activeBridge: SensorBridgeHandle | null = null
export function peekLatestLocalReading(): { reading: MobileSensorReading | null; status: BridgeStatus }
```

`startSensorBridge` assigns `activeBridge` (and tracks a coarse status: 'unsupported' |
'awaiting-permission' | 'capturing' | 'stopped'). `peekLatestLocalReading()` returns
`activeBridge?.latest() ?? null` plus the status, or `{ reading: null, status: 'not-started' }`
if the bridge was never started. `main.ts` keeps calling `startSensorBridge(...)`; no posting
behavior changes. The mobile page polls this accessor on a ~1 s interval.

## States (every live page)

- **Loading** (no data yet): "Loading…".
- **Error**: prominent banner with the error message and HTTP status (from `meta.httpStatus`);
  stale data, if any, remains visible beneath.
- **Empty/null**: explicit per category — disks "no drives reported", networks "no interfaces",
  battery "no battery present", mobile "no devices reporting".
- **Static** (`/info`): load once, Refresh button, no live indicator/pause.

## Testing

The frontend has no test harness today, and these pages *are* a manual verification surface.

- **Automated:** `vue-tsc -b` type-check must pass for all new code. **No vitest** is added
  for v1 (decision: avoid net-new FE test infra).
- **Manual verification checklist** (run with the API up, `npm run dev`):
  1. Nav "Hardware" link opens the hub; hub links reach each page and back.
  2. Each live page shows values updating, a sensible "fetched … ago", and HTTP 200.
  3. Pause stops updates (indicator → ⏸); Resume restarts; Refresh now forces a fetch.
  4. Raw-JSON toggle reveals JSON matching the rendered fields.
  5. Empty/null states render (battery null on a desktop with no battery; disks/networks
     populated; mobile "no devices" until a client posts).
  6. `/hardware/info` is static with a working Refresh and no pause control.
  7. Mobile page: with a browser/device posting via the bridge, the "this client" panel shows
     a live local reading and a status; cached devices list appears after a poll cycle.
  8. Stop the API → pages show the error banner with a status, not a blank/crash.

## Out of scope (YAGNI, restated)

Charts/history, alerting/thresholds, export, editing hardware state, new backend endpoints,
auth gating (the launcher is single-user/local; existing auth scaffolding is unrelated and
untouched), and any change to `SensorsPanel.vue`/`HomeView` beyond the additive store metadata.
