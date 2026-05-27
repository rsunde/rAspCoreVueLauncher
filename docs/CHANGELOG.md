# Changelog

Feature-level history of `rAspCoreVueLauncher`, newest first. Grouped by commit; not a literal git log. For the current state, see [`HANDOVER.md`](HANDOVER.md). For what's coming, see [`ROADMAP.md`](ROADMAP.md).

## 2026-05-27 — Interactive wizard + VS Code launch profiles (`9a09ec2`)

- Added `scripts/wizard.mjs` driven by `@clack/prompts`, exposing Run / Build / Package / Setup / Clean menus from a single `npm run wizard` entry point.
- Package menu opens the host file explorer at the artifact folder after a successful Tauri or Capacitor build, so the user can grab the MSI / APK without hunting through `target/` or `outputs/`.
- Rebuilt `.vscode/launch.json` and `.vscode/tasks.json` around 22 launch entries split across four presentation groups — `1_app` compounds, `2_dev` per-process, `3_test`, `4_workspace` orchestrators — so F5 surfaces the right thing for whichever role you're playing.
- Added `@clack/prompts` to root `package.json` as the only runtime dependency the workspace itself has.

## 2026-05-27 — Cross-platform build & package scripts (`584704f`)

- Added `scripts/setup.ps1` and `scripts/setup.sh` host checks. Verify-only by design: they detect missing toolchains and print install hints (winget on Windows, distro-aware apt/dnf/pacman on Linux) but never install anything themselves. Both accept `-SkipAndroid` / `--skip-android` and `-SkipDesktop` / `--skip-desktop`.
- Added the root `package.json` workspace entry points: `npm run setup | build | test | clean | package | package:desktop | package:android | package:ios`.
- Implemented the orchestrators as zero-dep Node scripts under `scripts/` (`build.mjs`, `test.mjs`, `clean.mjs`, `package-*.mjs`).
- Added `scripts/lib/run.mjs` with a Windows `.cmd` shim so `npm`, `npx`, `cap`, and `tauri` resolve correctly when spawned cross-platform.
- `package:ios` and macOS desktop bundles exit cleanly with a "requires a macOS host" notice on Windows/Linux instead of failing noisily.

## 2026-05-27 — Mobile sensor ingest + drop-in client bridge (`bd0c356`)

- Designed the `MobileSensorReading` DTO graph in `src/rAspCoreVueLauncher.Shared/Hardware/HardwareSensors.cs`: motion, orientation, environment, location, health, biometric, connectivity, and UI groups, all nullable so clients only send what their platform exposes.
- Added `POST /api/hardware/sensors/mobile` ingest plus a `MobileSensorCache` singleton; the latest reading is echoed back under the `mobile` field on `GET /api/hardware/sensors`.
- Built `src/rAspCoreVueLauncher.Web/src/lib/sensorsBridge.ts` — a zero-dep drop-in for BYO Vue apps. Wires `DeviceMotion`, `DeviceOrientation`, `Geolocation`, Battery, and `NetworkInformation` to the ingest endpoint, with `startSensorBridge()` as the one-call entry point and a stable `clientId` cached in `localStorage`.
- Added 3 MSTest contract tests for the ingest endpoint and the `/sensors` echo behavior (total now 6/6 passing).
- Wrote [`docs/BYO-APP.md`](BYO-APP.md): adoption paths, options reference, iOS permission gotcha, Capacitor manifest checklist, CORS troubleshooting.
- Wrote [`AGENTS.md`](../AGENTS.md) with working-style directives (sub-agent delegation, session-budget wakeup pattern, lowercase-`r` naming rule).

## 2026-05-27 — Cross-platform template baseline (`5281008` · initial commit)

- Stood up the ASP.NET Core 10 minimal API: EF Core + SQLite, ASP.NET Identity, JWT bearer auth, Scalar docs at `/scalar/v1`, seeded `dev@example.com / Dev!2345` user.
- Stood up the Vue 3 frontend: Vite + TypeScript + Tailwind v4 + shadcn-vue + Pinia + Vue Router, with a `/api` Vite dev proxy to the API.
- Added Tauri 2 desktop shell under `src/rAspCoreVueLauncher.Web/src-tauri/` (Rust crate + `tauri.conf.json`). Sidecar wiring intentionally deferred.
- Added Capacitor 7+ mobile shell as `capacitor.config.ts`. Native `android/` and `ios/` folders are not committed; they're generated on demand by `npx cap add`.
- Wired the MSTest contract test project (`tests/rAspCoreVueLauncher.Api.Tests/`) with `WebApplicationFactory`, NSubstitute, FluentAssertions v6.
- Initial `.vscode/launch.json` with a basic API + Vue compound to make F5 work out of the box.

---

**Future entries:** add the next change at the top when you ship it. Format: commit hash + date + tagline as the heading, then 3–5 bullets describing what changed semantically (not a diff dump).
