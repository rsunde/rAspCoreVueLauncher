# rAspCoreVueLauncher

A runnable template for cross-platform apps built on ASP.NET Core 10 + Vue 3, wrapped with **Tauri** for desktop and **Capacitor** for mobile. Vue is the single UI; the ASP.NET API exposes a `IHardwareService` abstraction so the same web frontend can reach native sensors from any wrapper.

This repo is a living base — clone it, rename it, build the next app.

## Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| Backend | ASP.NET Core 10 Minimal API + EF Core + SQLite + JWT + Identity | REST API, auth, persistence |
| Shared | .NET 10 class library | DTOs, contracts, seed data |
| Frontend | Vue 3 + Vite + TS + Tailwind v4 + shadcn-vue + Pinia + Vue Router | The UI |
| Desktop shell | Tauri 2 | Native window + hardware bridge |
| Mobile shell | Capacitor 7+ | iOS / Android packaging |
| Tests | MSTest + NSubstitute + FluentAssertions v6 | Contract + integration |

## Layout

```
src/
  rAspCoreVueLauncher.Shared/      # contracts shared between API and tests
  rAspCoreVueLauncher.Api/         # ASP.NET Core 10 minimal API
  rAspCoreVueLauncher.Web/         # Vue 3 app + Tauri + Capacitor config
    src/                           # Vue source
    src-tauri/                     # Tauri desktop shell (Rust)
    capacitor.config.ts            # Capacitor mobile config
tests/
  rAspCoreVueLauncher.Api.Tests/   # MSTest + WebApplicationFactory
.vscode/                           # launch.json, tasks.json, extensions.json
```

## Prerequisites

- .NET 10 SDK (`10.0.300` pinned via `global.json`)
- Node.js 22+
- **Optional, for desktop builds:** Rust toolchain (https://rustup.rs)
- **Optional, for mobile builds:** Android Studio (Android) and/or Xcode on macOS (iOS)

## First-time setup

```pwsh
# .NET
dotnet restore

# Web
cd src/rAspCoreVueLauncher.Web
npm install
```

## Run it

### From VS Code (recommended)

Open the folder and press **F5**. Pick a compound configuration:

- **Full Stack: API + Vue** — runs the ASP.NET API (with hot reload) plus the Vite dev server. Visit http://localhost:5173.
- **Full Stack: API + Tauri** — same, but the Vue app opens inside a Tauri desktop window. Requires Rust.

Individual configurations are also available (API only, Web only, Tauri only).

### From the terminal

```pwsh
# Terminal 1 — API at https://localhost:7102 (Scalar UI at /scalar/v1)
dotnet watch --project src/rAspCoreVueLauncher.Api run --launch-profile https

# Terminal 2 — Vue dev server at http://localhost:5173 (proxies /api to the backend)
cd src/rAspCoreVueLauncher.Web
npm run dev
```

### Tests

```pwsh
dotnet test
```

## Auth

The API seeds a single dev user on first run:

| Email              | Password   |
|--------------------|------------|
| `dev@example.com`  | `Dev!2345` |

`POST /api/auth/login` returns a JWT bearer token. `GET /api/auth/me` requires it. The signing key in `appsettings.Development.json` is for local dev only — production must override `Jwt:SigningKey` via environment variable or secret store.

## Hardware abstraction

`IHardwareService` in `rAspCoreVueLauncher.Shared` defines what the Vue frontend can ask about the device. Today: OS, architecture, machine name, cores, memory. Add sensors here as the platform implementations grow.

- **Desktop (Tauri)**: the ASP.NET API runs as a sidecar process. Most hardware is reachable via .NET directly.
- **Mobile (Capacitor)**: things .NET can't reach from a webview process (camera, geolocation, accelerometer) will need a Capacitor plugin proxied through the same `IHardwareService` contract. Add plugin-backed implementations under `src/rAspCoreVueLauncher.Web/src-tauri` (for Tauri commands) or via Capacitor plugins, then expose them to Vue under the same shape the backend uses.

## Desktop bundle (Tauri)

```pwsh
cd src/rAspCoreVueLauncher.Web
npm run tauri:dev      # dev with hot reload
npm run tauri:build    # produces installers for the current OS
```

To ship the API alongside the desktop binary, publish a self-contained build and reference it as a Tauri `externalBin` sidecar in `src-tauri/tauri.conf.json`. (TODO — not wired in this template.)

## Mobile bundle (Capacitor)

iOS and Android platforms aren't checked in — generate them on demand:

```pwsh
cd src/rAspCoreVueLauncher.Web
npm run build                       # produce dist/
npx cap add android                 # one-time; creates android/ folder
npx cap add ios                     # one-time; macOS + Xcode required
npm run cap:sync                    # copy dist/ into native projects
npm run cap:android                 # open Android Studio / run on device
npm run cap:ios                     # open Xcode / run on simulator
```

For mobile, the Vue app is served from the device — it must talk to a remote ASP.NET API rather than localhost. Configure the API base URL via Vite env vars before building.

## What's not here yet

- Tauri sidecar wiring to ship the ASP.NET binary inside the desktop bundle
- iOS / Android Capacitor platforms (added on demand)
- A real production JWT key strategy
- EF Core migrations (uses `EnsureCreated` today; swap to `MigrateAsync` once you add `dotnet ef migrations`)
