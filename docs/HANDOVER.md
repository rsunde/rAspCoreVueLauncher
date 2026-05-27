# Handover — current state of rAspCoreVueLauncher

A snapshot of where this repo is right now, written so the next contributor (human or AI) can be productive in ~10 minutes without rereading git history. Pair this with [`CHANGELOG.md`](CHANGELOG.md) for the "what happened" view and [`ROADMAP.md`](ROADMAP.md) for the "what's next" view.

## Snapshot

`rAspCoreVueLauncher` is a runnable cross-platform app template: ASP.NET Core 10 minimal API + Vue 3 single-page UI, packaged for desktop with Tauri 2 and for mobile with Capacitor 7. On top of the initial commit baseline, two layers have been added: (1) a mobile-sensor ingest pipeline with a zero-dep `sensorsBridge.ts` drop-in for any Vue host, and (2) a cross-platform workflow surface — host setup scripts, Node orchestrators, a `@clack/prompts` wizard, and 22 VS Code launch configs across four presentation groups.

The repo is intentionally a base to fork from. The bundled Vue app is a placeholder; see [`BYO-APP.md`](BYO-APP.md) for grafting your own app onto the shell.

## What's shipped

### API — `src/rAspCoreVueLauncher.Api/`

ASP.NET Core 10 minimal API with EF Core + SQLite, Identity, JWT bearer auth, and Scalar docs at `/scalar/v1`.

| Concern | Where |
|---------|-------|
| Composition | `Program.cs` (DI, CORS `VueDevCors`, auth, endpoint registration) |
| Auth endpoints | `Auth/AuthEndpoints.cs` — `POST /api/auth/login`, `GET /api/auth/me` |
| Hardware endpoints | `Hardware/HardwareEndpoints.cs` — `GET /api/hardware/info`, `GET /api/hardware/sensors`, `POST /api/hardware/sensors/mobile` |
| `IHardwareService` impl | `Hardware/HardwareService.cs` — `GC.GetGCMemoryInfo()`, `DriveInfo`, `NetworkInterface` |
| Mobile sensor cache | `Hardware/MobileSensorCache.cs` — singleton, latest-wins |
| Seeded dev user | `dev@example.com / Dev!2345` (first run) |

### Shared — `src/rAspCoreVueLauncher.Shared/`

.NET 10 class library. Contracts only — no behavior. The mobile sensor DTO graph lives in `Hardware/HardwareSensors.cs` (`MobileSensorReading`, `MotionSensors`, `OrientationSensors`, `EnvironmentSensors`, `LocationSensors`, `HealthSensors`, `BiometricSensors`, `ConnectivitySensors`, `UserInterfaceSensors`, `MobileDeviceInfo`, `Vector3`, `Vector4`).

### Web — `src/rAspCoreVueLauncher.Web/`

Vue 3 + Vite + TypeScript + Tailwind v4 + shadcn-vue + Pinia + Vue Router.

| Concern | Where |
|---------|-------|
| Entry | `src/main.ts` |
| Axios client | `src/api/client.ts` — same-origin baseURL |
| Hardware TS types | `src/types/hardware.ts` (hand-written today; codegen TODO is in-file) |
| Sensors drop-in | `src/lib/sensorsBridge.ts` — `startSensorBridge()` posts every 2 s |
| Vite dev proxy | `vite.config.ts` — `/api` → `http://localhost:5148` |

### Tauri desktop shell — `src/rAspCoreVueLauncher.Web/src-tauri/`

Tauri 2 config + Rust crate. The Vue bundle is loaded into a native window. The API is **not** yet wired as an `externalBin` sidecar — that work is the first item in [`ROADMAP.md`](ROADMAP.md).

### Capacitor mobile shell — `src/rAspCoreVueLauncher.Web/capacitor.config.ts`

Config only. Native `android/` and `ios/` folders are intentionally **not** committed; `npm run package:android` runs `npx cap add android` on demand. iOS requires a macOS host and is skipped cleanly elsewhere.

### Tests — `tests/rAspCoreVueLauncher.Api.Tests/`

MSTest + NSubstitute + FluentAssertions v6 + `WebApplicationFactory`. **6 tests**, all passing on Windows. Each test builds a fresh `TestAppFactory`, so cache isolation works today.

### Setup scripts — `scripts/setup.ps1`, `scripts/setup.sh`

Verify-only host checks. They **do not install** anything; they print install hints. Both accept `-SkipAndroid` / `--skip-android` and `-SkipDesktop` / `--skip-desktop`. The Linux script is distro-aware (apt/dnf/pacman hints).

### Node orchestrator scripts — `scripts/*.mjs`

Zero-dep (except for the wizard's `@clack/prompts`). Cross-platform: a `.cmd` shim in `scripts/lib/run.mjs` handles Windows resolution for `npm`/`npx`/`cap`/`tauri`.

| Script | Purpose |
|--------|---------|
| `setup-dispatch.mjs` | Picks PowerShell or Bash setup script for the host |
| `build.mjs` | `dotnet build` + Vite build |
| `test.mjs` | `dotnet test` |
| `clean.mjs` | Wipes `bin/`, `obj/`, `dist/`, `target/`, `build/`; `--deep` also wipes `node_modules/` |
| `package-desktop.mjs` | Tauri bundle for the current host |
| `package-android.mjs` | Capacitor + Gradle release APK |
| `package-ios.mjs` | Prints "requires macOS" and exits 0 off macOS |
| `package-all.mjs` | Runs every packager available on this host |
| `wizard.mjs` | Interactive `@clack/prompts` menu |
| `lib/run.mjs` | Spawn helper with Windows `.cmd` resolution |

### Interactive wizard — `scripts/wizard.mjs`

`npm run wizard` opens a top-level menu: **Run / Build / Package / Setup / Clean / Quit**. Each subcommand drills into the same Node scripts above. Packaging reveals artifacts in the host file explorer when done.

### VS Code launch profiles — `.vscode/launch.json`, `.vscode/tasks.json`

22 launch entries grouped by presentation:

| Group | Entries |
|-------|---------|
| `1_app` | 5 compounds (`Full Stack: API (HTTP) + Vue`, `… + Tauri`, `… + Capacitor Android`, `… + Web Preview`, `API (HTTPS) + Vue`) |
| `2_dev` | API HTTP / HTTPS, Web Vite dev, Web Preview, Tauri dev, Tauri Build, Capacitor Android, Capacitor iOS |
| `3_test` | `API: Run Tests`, `API: Attach` |
| `4_workspace` | Wizard, Setup check, Build All, Test All, Clean, Package: Desktop, Package: Android, Package All |

`tasks.json` provides matching `api: build`, `web: build`, and orchestrator pre-launch tasks. All JSON parses strict.

### Project-level docs

- [`README.md`](../README.md) — quickstart and host matrix.
- [`AGENTS.md`](../AGENTS.md) — agent working-style directives (sub-agent delegation, wakeup budget, naming rule).
- [`docs/BYO-APP.md`](BYO-APP.md) — grafting your Vue app onto this shell, sensor wiring, iOS permission gotcha, troubleshooting.

## Verified state

What was actually exercised on the dev host (Windows 11) vs. what was written but not run:

| Action | Status |
|--------|--------|
| `dotnet build` | VERIFIED — succeeds |
| `dotnet test` | VERIFIED — 6/6 passing (run before a VS debug session held a file lock) |
| `scripts/setup.ps1` end-to-end | VERIFIED — flags missing tools correctly on Windows |
| TypeScript type-check (`tsc --noEmit` via Vite) | VERIFIED — passes |
| Wizard imports resolve | VERIFIED |
| `launch.json` + `tasks.json` strict JSON parse | VERIFIED |
| `scripts/setup.sh` on Linux | NOT EXERCISED — no Linux host available |
| `npm run package:desktop` | NOT EXERCISED — Rust not installed on dev host |
| `npm run package:android` | NOT EXERCISED — no Android SDK on dev host |
| Capacitor builds | NOT EXERCISED |
| Tauri builds | NOT EXERCISED |
| `npm run wizard` end-to-end | NOT EXERCISED — only import resolution checked |
| Pressing F5 in VS Code on each launch config | NOT EXERCISED — JSON valid, runtime untested |

## Known caveats

Design choices that look like bugs but are intentional. Read these before "fixing" them.

- **Wizard's "Full stack" option prints a two-terminal recipe** instead of forking processes. `@clack/prompts` owns the TTY; spawning two long-lived children alongside it causes ANSI corruption and unkillable processes. The VS Code compound is the supported path.
- **`API: Run Tests` launch config runs `dotnet test`** but won't break on breakpoints in test code — proper test debugging uses the C# extension's CodeLens "Debug Test" buttons. The config is for one-shot runs only.
- **`MobileSensorCache` is a single-field, latest-wins cache.** No per-client history. Adequate for current use; a future agent may want a `Dictionary<clientId, reading>` with eviction.
- **Tests don't swap `IMobileSensorCache` in `TestAppFactory`.** Each test creates a fresh factory, so isolation works today. If MSTest were ever configured to share factories across tests, cache state would leak. Flagged here, not fixed.
- **`sensorsBridge.ts` does NOT fill `health` or `biometric` blocks.** Those DTOs exist server-side, but populating them needs Capacitor plugins (HealthKit / Google Fit / `LocalAuthentication`). The bundled bridge is web-API-only.
- **`BatterySnapshot` is hardcoded to `null` server-side.** No cross-platform .NET Battery API; would need WMI on Windows / `/sys/class/power_supply` on Linux. See [`ROADMAP.md`](ROADMAP.md).
- **`HardwareService.GetSensorsAsync` uses `GC.GetGCMemoryInfo()`.** The "total available memory" number reflects GC limits, not OS RAM. Acceptable for the demo; swap to a platform-specific call if you need true host RAM.
- **EF Core uses `EnsureCreated`, not migrations.** Fine for the template; replace with `dotnet ef migrations` + `MigrateAsync` once the schema starts to move.

## Repo tour

Most important paths. Skips `bin/`, `obj/`, `node_modules/`, `target/`, `dist/`.

```
rAspCoreVueLauncher/
├── AGENTS.md                                # Agent working-style + naming rule
├── README.md                                # Quickstart, host matrix, package commands
├── package.json                             # Root npm scripts → scripts/*.mjs
├── global.json                              # .NET 10.0.300 pin
├── rAspCoreVueLauncher.slnx                 # Solution
├── docs/
│   ├── BYO-APP.md                           # Grafting your Vue app onto the shell
│   ├── HANDOVER.md                          # This file
│   ├── CHANGELOG.md                         # Feature history
│   └── ROADMAP.md                           # What's next
├── .vscode/
│   ├── launch.json                          # 22 configs, 4 groups
│   ├── tasks.json                           # api:build, web:build, orchestrators
│   └── extensions.json
├── scripts/
│   ├── setup.ps1                            # Windows verify-only host check
│   ├── setup.sh                             # Linux verify-only host check
│   ├── setup-dispatch.mjs                   # Picks the right setup script
│   ├── build.mjs / test.mjs / clean.mjs
│   ├── package-desktop.mjs / -android.mjs / -ios.mjs / -all.mjs
│   ├── wizard.mjs                           # @clack/prompts menu
│   └── lib/run.mjs                          # Cross-platform spawn helper
├── src/
│   ├── rAspCoreVueLauncher.Shared/
│   │   └── Hardware/HardwareSensors.cs      # DTO graph (mobile + server snapshots)
│   ├── rAspCoreVueLauncher.Api/
│   │   ├── Program.cs                       # DI, CORS, auth, endpoint wiring
│   │   ├── Auth/AuthEndpoints.cs            # /api/auth/login, /api/auth/me
│   │   ├── Hardware/HardwareEndpoints.cs    # /api/hardware/{info,sensors,sensors/mobile}
│   │   ├── Hardware/HardwareService.cs      # IHardwareService impl
│   │   └── Hardware/MobileSensorCache.cs    # Singleton, latest-wins
│   └── rAspCoreVueLauncher.Web/
│       ├── src/
│       │   ├── main.ts                      # Vue entry (startSensorBridge NOT wired yet)
│       │   ├── api/client.ts                # Axios, same-origin
│       │   ├── types/hardware.ts            # Hand-written; codegen TODO
│       │   └── lib/sensorsBridge.ts         # Zero-dep drop-in
│       ├── vite.config.ts                   # /api dev proxy → :5148
│       ├── capacitor.config.ts              # Mobile config (no native folders committed)
│       └── src-tauri/                       # Tauri 2 desktop shell
└── tests/
    └── rAspCoreVueLauncher.Api.Tests/       # 6 MSTest tests, all passing
```

## How to pick this up

1. **Read [`AGENTS.md`](../AGENTS.md).** Naming rule (lowercase `r` prefix everywhere) and sub-agent delegation guidance.
2. **Run the host check.** `pwsh scripts/setup.ps1` on Windows or `./scripts/setup.sh` on Linux. It prints what's missing — install what you need and re-run.
3. **Pick an entry point:**
   - VS Code: open the folder, hit **F5**, pick a compound from the `1_app` group.
   - Terminal: `npm run wizard` for the interactive menu.
   - Manual: `dotnet watch --project src/rAspCoreVueLauncher.Api run` in one terminal, `npm run dev` in `src/rAspCoreVueLauncher.Web` in another.
4. **For AI agents:** durable memory lives in `C:\Users\Ronnie\.claude\projects\X--git-rsunde-rAspCoreVueLauncher\memory\`. Start with `MEMORY.md` there.
5. **Pick the next change** from [`ROADMAP.md`](ROADMAP.md). The recommended sequence starts with Tauri sidecar wiring.

When you ship something, add an entry to the top of [`CHANGELOG.md`](CHANGELOG.md) following the existing format.
