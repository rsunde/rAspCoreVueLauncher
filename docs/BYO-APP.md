# Bring your own Vue app

This template is a runnable shell for cross-platform apps: an ASP.NET Core API, a Tauri desktop wrapper, a Capacitor mobile wrapper, and a mobile-sensor ingest pipeline. The bundled Vue 3 project is a placeholder. If you already have a Vue app, this guide shows how to graft it onto the shell and how to wire up sensor reporting with a single import.

## What this repo adds to a plain Vue app

| Concern | Plain Vue | This repo |
|---------|-----------|-----------|
| HTTP backend | none | ASP.NET Core 10 minimal API (`rAspCoreVueLauncher.Api`) |
| Auth | none | JWT + EF Core Identity, dev user seeded |
| Desktop packaging | none | Tauri 2 (`src-tauri/`) |
| Mobile packaging | none | Capacitor 7+ (`capacitor.config.ts`) |
| Mobile sensor ingest | none | `POST /api/hardware/sensors/mobile` + drop-in `sensorsBridge.ts` |
| Shared DTOs | none | `rAspCoreVueLauncher.Shared` class library |

The contract is one-way: the Vue app talks to the API over HTTP. Tauri and Capacitor just package the Vue app. You don't need to learn Rust or write Capacitor plugins for the bundled sensor pipeline.

## Quickstart: 5 minutes

```pwsh
# 1. Clone the template.
git clone https://github.com/<you>/rAspCoreVueLauncher my-app
cd my-app

# 2. Swap the bundled Vue project for yours (see "Path A" below).
#    Keep src-tauri/ and capacitor.config.ts where they are.

# 3. Drop the sensor bridge into your Vue source.
copy path\to\template\src\rAspCoreVueLauncher.Web\src\lib\sensorsBridge.ts `
     src\rAspCoreVueLauncher.Web\src\lib\sensorsBridge.ts

# 4. Add one import + one call to your Vue entry (main.ts).
#    See "Wiring up sensors" below.

# 5. Install and run.
cd src\rAspCoreVueLauncher.Web
npm install
cd ..\..
# Press F5 in VS Code → pick "Full Stack: API + Vue".
```

You now have your existing Vue app running against the ASP.NET API, with mobile sensor readings posting to `/api/hardware/sensors/mobile` every 2 seconds when accessed from a device that exposes them.

## Build & package your app

Once your Vue code is in place, build and package from the repo root with the workspace npm scripts:

```pwsh
pwsh scripts/setup.ps1     # Windows host check (or ./scripts/setup.sh on Linux)
npm run build              # dotnet build + Vue build
npm run test               # dotnet test
npm run package:desktop    # Tauri bundle (Windows MSI / Linux deb + AppImage)
npm run package:android    # Capacitor + Gradle release APK (needs ANDROID_HOME, JDK 17)
npm run package            # everything available on this host
```

`npm run package:ios` and the macOS desktop bundle are skipped on Windows/Linux hosts — they exit cleanly with a "requires a macOS host" message. See the root [`README.md`](../README.md#build--package) for the full host matrix.

## Adoption paths

There are two ways to combine your code with this template. Pick whichever leaves the smaller diff.

### Path A — Replace the bundled Vue app

Use this when the template is the project skeleton and your Vue app is the content.

1. Delete the placeholder Vue source under `src/rAspCoreVueLauncher.Web/src/` and `src/rAspCoreVueLauncher.Web/public/`.
2. Copy these from your existing Vue app into `src/rAspCoreVueLauncher.Web/`:
   - `src/` (your components, views, stores, etc.)
   - `public/`
   - `index.html`
   - `package.json` dependencies (merge — keep the template's `devDependencies` for `@tauri-apps/*` and `@capacitor/*`)
   - `tsconfig.json` (merge — keep paths the template needs)
   - `vite.config.ts` — **must keep the `/api` dev proxy**, see below
3. Leave these alone:
   - `src-tauri/` (Tauri desktop shell, including `tauri.conf.json`)
   - `capacitor.config.ts` (Capacitor mobile config)
   - The `tauri:*` and `cap:*` npm scripts in `package.json`
4. Run `npm install` inside `src/rAspCoreVueLauncher.Web`.

The `vite.config.ts` proxy is what lets your Vue app call `/api/...` in dev without CORS:

```ts
// src/rAspCoreVueLauncher.Web/vite.config.ts
export default defineConfig({
  // ...your config
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5148',
        changeOrigin: true,
      },
    },
  },
})
```

If your Vue app uses a different dev port, also add it to the CORS allowlist in `src/rAspCoreVueLauncher.Api/Program.cs` (`VueDevCors`).

### Path B — Migrate this template into your repo

Use this when your repo is already the project root and you want to add the API + native shells alongside what you have.

Copy these from the template into your repo:

| Source | Destination | Notes |
|--------|-------------|-------|
| `src/rAspCoreVueLauncher.Api/` | `src/rAspCoreVueLauncher.Api/` | The ASP.NET API |
| `src/rAspCoreVueLauncher.Shared/` | `src/rAspCoreVueLauncher.Shared/` | DTO contracts |
| `tests/` | `tests/` | MSTest projects |
| `rAspCoreVueLauncher.slnx` | repo root | Or merge into your existing `.sln`/`.slnx` |
| `global.json` | repo root | Pins the .NET 10 SDK |
| `src/rAspCoreVueLauncher.Web/src-tauri/` | next to your Vue project | Tauri reads from a sibling `dist/` |
| `src/rAspCoreVueLauncher.Web/capacitor.config.ts` | next to your Vue project | `webDir: 'dist'` |
| `src/rAspCoreVueLauncher.Web/src/lib/sensorsBridge.ts` | your Vue `src/lib/` | The bridge |
| (relevant) `tauri:*` and `cap:*` scripts | your `package.json` | Lets you `npm run tauri:dev` etc. |

Folder shape after migration:

```
your-repo/
  src/
    rAspCoreVueLauncher.Api/        # API runs independently
    rAspCoreVueLauncher.Shared/
    YourVueApp/                      # your existing Vue project
      src-tauri/                     # added from template
      capacitor.config.ts            # added from template
      src/lib/sensorsBridge.ts       # added from template
  tests/
  global.json
  YourSolution.slnx
```

The API project must sit so it can be launched independently of the Vue project — i.e. `dotnet run --project src/rAspCoreVueLauncher.Api` works from the repo root. The Vue project is a sibling, not a child.

You may rename `rAspCoreVueLauncher.Api`, `rAspCoreVueLauncher.Shared`, and the namespace inside if you want a tidier name. Update the project references in `.slnx` and the `using rAspCoreVueLauncher.Shared.Hardware;` lines in the API.

## What you DON'T need to change

> Sidebar — leave these alone unless you have a reason.
>
> - `src/rAspCoreVueLauncher.Api/` — endpoints, services, DI wiring
> - `src/rAspCoreVueLauncher.Shared/Hardware/HardwareSensors.cs` — the DTO contract
> - The HTTP shape: `GET /api/hardware/info`, `GET /api/hardware/sensors`, `POST /api/hardware/sensors/mobile`
> - The `sensorsBridge.ts` payload shape — it already matches the server DTO
>
> The bridge and the server agree on field names by convention (camelCase on the wire, PascalCase in the C# record). Don't rename one without renaming the other.

## Wiring up sensors

### One-line integration

Copy `src/rAspCoreVueLauncher.Web/src/lib/sensorsBridge.ts` into your Vue project at the same path, then add one import and one call in `main.ts`:

```ts
// src/main.ts
import { createApp } from 'vue'
import App from './App.vue'
import { startSensorBridge } from './lib/sensorsBridge'

createApp(App).mount('#app')

startSensorBridge()  // posts every 2s to /api/hardware/sensors/mobile
```

That's it. The bridge:

- Generates a stable `clientId` and persists it in `localStorage` (`rAspCoreVueLauncher:sensorBridge:clientId`).
- Subscribes to `devicemotion`, `deviceorientation`, `navigator.geolocation.watchPosition`, `navigator.getBattery`, and `navigator.connection` where the browser/WebView exposes them.
- Builds a `MobileSensorReading` and POSTs it on a timer (default 2000ms).
- Silently skips sensor groups the platform doesn't expose.

### Options

`startSensorBridge(opts?: SensorBridgeOptions)`:

| Option | Default | Purpose |
|--------|---------|---------|
| `apiBaseUrl` | `''` (same-origin) | Override when the API is on a different host (e.g. mobile dev against a LAN IP). |
| `clientId` | auto-generated + cached in localStorage | Stable identifier for this install. Pass your own if you have one. |
| `intervalMs` | `2000` | Minimum 250ms. |
| `enable` | all `true` | Per-group toggle: `motion`, `orientation`, `location`, `battery`, `connectivity`, `device`. |
| `onPosted` | undefined | Callback after every successful POST — receives the reading. |
| `onError` | `console.warn` | Callback for POST failures. |

Example — point a Capacitor build at a LAN-hosted dev API, and tick faster:

```ts
import { startSensorBridge } from './lib/sensorsBridge'

const handle = startSensorBridge({
  apiBaseUrl: 'http://192.168.1.42:5148',
  intervalMs: 1000,
  onPosted: (r) => console.debug('posted', r.capturedAtUtc),
})

// Later, if you need to stop:
// handle.stop()
```

The returned handle also exposes `flush()` to post immediately and `latest()` to read the most recent reading the bridge built.

## Data contract

The wire format is defined by [`src/rAspCoreVueLauncher.Shared/Hardware/HardwareSensors.cs`](../src/rAspCoreVueLauncher.Shared/Hardware/HardwareSensors.cs). Quick map:

| Group | Server DTO | Notes |
|-------|-----------|-------|
| Identity | `MobileSensorReading.ClientId`, `CapturedAtUtc` | Required. Bridge sets both. |
| Device | `MobileDeviceInfo` | UA, locale, time zone from the browser. |
| Motion | `MotionSensors` (`Vector3`/`Vector4`) | Filled from `DeviceMotionEvent`. |
| Orientation | `OrientationSensors` | Filled from `DeviceOrientationEvent`. |
| Environment | `EnvironmentSensors` | Browser sensors API — rarely exposed; mostly null. |
| Location | `LocationSensors` | Filled from `navigator.geolocation.watchPosition`. |
| Health | `HealthSensors` | Not filled by the bundled bridge. Plugin work. |
| Biometric | `BiometricSensors` | Not filled by the bundled bridge. Plugin work. |
| Connectivity | `ConnectivitySensors` | Partial — `effectiveType`, `saveData`. |
| UI | `UserInterfaceSensors` | Not filled by the bundled bridge. |

The server is a pass-through: `POST /api/hardware/sensors/mobile` validates `clientId` and stores the latest reading in an in-memory `IMobileSensorCache`. `GET /api/hardware/sensors` then includes that reading in its response under the `mobile` field. There is no DB persistence today.

```http
POST /api/hardware/sensors/mobile HTTP/1.1
Content-Type: application/json

{
  "clientId": "web-3f9a...",
  "capturedAtUtc": "2026-05-27T12:34:56.789Z",
  "motion": { "accelerometer": { "x": 0.1, "y": 9.7, "z": 0.2 } }
}
```

Response: `202 Accepted` on success, `400` with a `clientId` validation error otherwise.

A fuller body from a phone might look like this — every block is optional, send only what you have:

```json
{
  "clientId": "web-3f9a4c0e-8b71-4d3e-9f12-1c2a8d5e0007",
  "capturedAtUtc": "2026-05-27T12:34:56.789Z",
  "device": {
    "model": "iPhone",
    "osName": "Mozilla/5.0 (iPhone; CPU iPhone OS 18_2 like Mac OS X) ...",
    "locale": "en-US",
    "timeZone": "Europe/Oslo"
  },
  "motion": {
    "accelerometer": { "x": 0.12, "y": 9.71, "z": 0.34 },
    "gyroscope":     { "x": 0.00, "y": 0.01, "z": 0.00 }
  },
  "orientation": {
    "yaw": 173.2, "pitch": 1.8, "roll": -0.4,
    "screenOrientation": "portrait-primary"
  },
  "location": {
    "latitude": 59.913, "longitude": 10.752,
    "accuracyMeters": 12.5,
    "fixTimestampUtc": "2026-05-27T12:34:55.000Z",
    "provider": "browser-geolocation"
  },
  "connectivity": { "networkType": "4g" }
}
```

The server is intentionally permissive: missing fields are stored as `null`, unknown fields are ignored.

## iOS permission gotcha

`DeviceMotionEvent.requestPermission` and `DeviceOrientationEvent.requestPermission` exist on iOS Safari/WKWebView. They MUST be called from inside a user-gesture handler (click, tap). The bridge calls them on startup, but if `startSensorBridge()` runs before the first user interaction, Safari rejects the prompt silently.

Recommended pattern for iOS: gate the bridge behind a "Start sensors" button.

```vue
<script setup lang="ts">
import { ref } from 'vue'
import { startSensorBridge, type SensorBridgeHandle } from './lib/sensorsBridge'

const handle = ref<SensorBridgeHandle | null>(null)
const start = () => { handle.value = startSensorBridge() }
</script>

<template>
  <button v-if="!handle" @click="start">Start sensors</button>
  <p v-else>Streaming…</p>
</template>
```

Android and desktop browsers don't gate motion/orientation behind a gesture, so `startSensorBridge()` at app startup is fine there.

## Capacitor permissions checklist

When you produce iOS/Android builds, the platform expects manifest entries even though `@capacitor/geolocation` and `@capacitor/motion` aren't strictly required for the bundled bridge (it uses the standard Web APIs that ship with WKWebView/WebView). Still, declare:

**Android — `android/app/src/main/AndroidManifest.xml`:**

```xml
<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" />
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
<uses-permission android:name="android.permission.HIGH_SAMPLING_RATE_SENSORS" />
<uses-permission android:name="android.permission.INTERNET" />
```

**iOS — `ios/App/App/Info.plist`:**

```xml
<key>NSLocationWhenInUseUsageDescription</key>
<string>Used to report device location to your account.</string>
<key>NSMotionUsageDescription</key>
<string>Used to report motion and orientation.</string>
```

Adjust the strings — App Store review reads them.

Health and biometric blocks (`HealthSensors`, `BiometricSensors`) exist in the DTO and the API will accept them, but the bundled `sensorsBridge.ts` does NOT fill them. That requires a real Capacitor plugin (HealthKit / Google Fit / `LocalAuthentication`) — future work, not in the template today.

## Troubleshooting

### CORS error: "blocked by CORS policy"

The API's dev CORS policy (`VueDevCors` in `src/rAspCoreVueLauncher.Api/Program.cs`) only lists:

- `http://localhost:5173`
- `http://localhost:4173`
- `tauri://localhost`
- `https://tauri.localhost`

If your Vue dev server runs on a different port or you serve a Capacitor build from a phone hitting your dev API by LAN IP, add the origin to that `WithOrigins(...)` list. In production, replace the dev policy entirely.

### 400 ValidationProblem: "clientId Required"

The bridge persists a generated id in `localStorage[rAspCoreVueLauncher:sensorBridge:clientId]`. In private/incognito tabs `localStorage` may throw and the bridge falls back to a per-session random id — still valid. If you see 400s, the body is likely empty or malformed; check that nothing strips `Content-Type: application/json` between the bridge and the API (some service workers will). Clearing the key forces a regenerate:

```js
localStorage.removeItem('rAspCoreVueLauncher:sensorBridge:clientId')
```

### No readings showing up on a desktop browser

`DeviceMotionEvent` and `DeviceOrientationEvent` only fire on devices with inertial sensors. Desktop Chrome/Edge will fire them only when you emulate sensors in DevTools (Sensors panel → set Orientation/Acceleration), or run inside a Chromium build with a USB IMU attached. The bridge is working — there's just nothing to report.

`navigator.getBattery` is also being removed from desktop browsers; expect `battery` to be undefined there.

### Tauri build can't reach the API

In production the Vue app inside Tauri runs at `tauri://localhost`. There is no Vite proxy in production, so `fetch('/api/...')` hits the Tauri scheme, not your backend. Either:

- Ship the API as a Tauri sidecar (see the template README — TODO note) and have it listen on `127.0.0.1:<port>`, then set `apiBaseUrl: 'http://127.0.0.1:<port>'` on the bridge and your axios client.
- Or set `apiBaseUrl` to a remote API URL.

### Mobile build hits localhost

Same issue: `localhost` inside a Capacitor app is the device itself. Use your dev machine's LAN IP (`apiBaseUrl: 'http://192.168.x.x:5148'`) and make sure the API binds to `0.0.0.0` rather than `localhost` — set `ASPNETCORE_URLS=http://0.0.0.0:5148` for the dev run, and add the LAN origin to the CORS allowlist.

## API endpoints you inherit

You don't have to write any of these — they're already wired up in `src/rAspCoreVueLauncher.Api/`. Hit them from your Vue app like any REST API.

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/api/auth/login` | Returns a JWT bearer token. |
| `GET` | `/api/auth/me` | Returns the current user — requires `Authorization: Bearer <jwt>`. |
| `GET` | `/api/hardware/info` | Static device facts: OS, cores, machine name. |
| `GET` | `/api/hardware/sensors` | Server-side sensors plus the latest `MobileSensorReading` echoed back. |
| `POST` | `/api/hardware/sensors/mobile` | Ingests a `MobileSensorReading` from the client. |

The `axios` instance the template ships at `src/rAspCoreVueLauncher.Web/src/api/client.ts` is a single-line same-origin client — copy or adapt it if you don't already have one:

```ts
import axios from 'axios'

export const api = axios.create({
  baseURL: '/',
  headers: { 'Content-Type': 'application/json' },
})
```

Same-origin works in dev (Vite proxy) and in Tauri (`tauri://localhost`). For Capacitor or a remote API, set `baseURL` to the absolute URL.

## Running the API and your Vue app together

The template assumes the API and the Vue dev server are launched as separate processes and the Vite proxy stitches them together over `/api`. In VS Code, `.vscode/launch.json` ships compound configs that do both at once. From the terminal:

```pwsh
# Terminal 1 — API on http://localhost:5148 (Scalar UI at /scalar/v1)
dotnet watch --project src/rAspCoreVueLauncher.Api run

# Terminal 2 — Vue dev server on http://localhost:5173
cd src/rAspCoreVueLauncher.Web
npm run dev
```

If you renamed the API project in Path B, update the `--project` path and the Vite proxy `target` to match. The default API port comes from `Properties/launchSettings.json` — keep `vite.config.ts`'s proxy `target` in sync.

For mobile/LAN testing, override the API base URL on the bridge and any HTTP clients you use, since `/api` won't proxy outside the Vite dev server:

```ts
const apiBase = import.meta.env.VITE_API_BASE_URL ?? ''
startSensorBridge({ apiBaseUrl: apiBase })
```

Then set `VITE_API_BASE_URL=http://192.168.1.42:5148` in `.env.development.local` (or whatever variant matches your build).

## Tauri vs Capacitor: where the bridge lives

Both wrappers serve the same Vue bundle, but the host context differs:

| Wrapper | App URL | Reaches API how |
|---------|---------|-----------------|
| Vite dev | `http://localhost:5173` | Vite proxies `/api` to `http://localhost:5148` |
| Tauri dev | `http://localhost:5173` (loaded inside Tauri window) | Same Vite proxy |
| Tauri prod | `tauri://localhost` | No proxy — set `apiBaseUrl` to the sidecar or remote URL |
| Capacitor dev | `http://localhost` inside WebView | No proxy — point at a LAN IP |
| Capacitor prod | `https://localhost` (Android) / app bundle | Point at your production API |

The bridge doesn't care which wrapper it runs in — it just uses standard browser APIs. If the wrapper exposes a richer sensor surface (e.g. a Capacitor Health plugin), call its API alongside `startSensorBridge` and merge the values into the same reading shape before POSTing.

## Reference

- API endpoints: [`src/rAspCoreVueLauncher.Api/Hardware/HardwareEndpoints.cs`](../src/rAspCoreVueLauncher.Api/Hardware/HardwareEndpoints.cs)
- Server DTOs: [`src/rAspCoreVueLauncher.Shared/Hardware/HardwareSensors.cs`](../src/rAspCoreVueLauncher.Shared/Hardware/HardwareSensors.cs)
- Sensor bridge: [`src/rAspCoreVueLauncher.Web/src/lib/sensorsBridge.ts`](../src/rAspCoreVueLauncher.Web/src/lib/sensorsBridge.ts)
- Vite dev proxy: [`src/rAspCoreVueLauncher.Web/vite.config.ts`](../src/rAspCoreVueLauncher.Web/vite.config.ts)
- CORS policy: `VueDevCors` in [`src/rAspCoreVueLauncher.Api/Program.cs`](../src/rAspCoreVueLauncher.Api/Program.cs)
