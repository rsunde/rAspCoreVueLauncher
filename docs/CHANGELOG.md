# Changelog

Feature-level history of `rAspCoreVueLauncher`, newest first. Grouped by commit; not a literal git log. For the current state, see [`HANDOVER.md`](HANDOVER.md). For what's coming, see [`ROADMAP.md`](ROADMAP.md).

## 2026-05-28 — Reframed as the Launcher; auth/EF Core stripped

- **Architectural pivot.** This repo is now positioned as the **Launcher** — a device-side template that exposes hardware/mobile sensors to whichever Vue app gets dropped into `src/rAspCoreVueLauncher.Web/`. Each Vue app keeps its own per-app API (auth, business data, persistence) in its own repo. `README.md` gained an "Architecture & terminology" section with a mermaid diagram and glossary.
- **Removed scaffolding from `rAspCoreVueLauncher.Api`:** deleted `Auth/`, `Data/`, `Migrations/`, the SQLite files, and the `dev@example.com` seed. Dropped NuGet refs to `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite`, and `Microsoft.EntityFrameworkCore.Tools`. `Program.cs` is now ~50 lines: CORS, hardware DI, OpenAPI, endpoints. `appsettings*.json` lost the `Jwt` and `ConnectionStrings` sections. `TestAppFactory` lost its `AppDbContext` substitution. 13/13 tests still pass.
- **Scalar "Try it" works out of the box.** `MobileSensorExampleTransformer` attaches a valid request example to the `IngestMobileSensors` OpenAPI operation so the auto-generated payload deserialises cleanly. `LenientDateTimeOffsetConverter` accepts ISO strings with or without a timezone offset (and Unix epoch numbers) as defence-in-depth for hand-rolled clients.
- **Per-clone port story.** Vite default bumped to **5174** and made env-overridable (`PORT=<n>` in `.env.local`), `tauri.conf.json` `devUrl` synced. API CORS predicate now accepts any `http(s)://localhost:*` origin so future clones can pick their own port without touching the API.

## 2026-05-27 — ROADMAP backlog sprint

- **Tauri sidecar wiring** — `lib.rs` spawns the ASP.NET API as a `tauri-plugin-shell` sidecar in release builds (`!cfg!(debug_assertions)`); kills it on window destroy. `tauri.conf.json` declares `externalBin`. `package-desktop.mjs` publishes a self-contained single-file binary (`dotnet publish -r <rid> --self-contained -p:PublishSingleFile=true`) and copies it to `src-tauri/binaries/rAspCoreVueLauncher-api-{triple}{ext}` before the Tauri bundle step. `VITE_API_BASE_URL=http://127.0.0.1:5148` is set for the production Vite build.
- **Sensor bridge wired** — `main.ts` now imports and calls `startSensorBridge({ apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? '' })` after mount.
- **`VITE_API_BASE_URL` env var** — `src/api/client.ts` reads `import.meta.env.VITE_API_BASE_URL ?? '/'`; `sensorsBridge` receives it from `main.ts`. `.env.production.example` added.
- **OpenAPI codegen script** — `scripts/generate-types.mjs` fetches `/openapi/v1.json` from the running API and generates `src/types/api.gen.ts` via `openapi-typescript`. Wired as `npm run gen:types`. `openapi-typescript` added as web devDep.
- **Auth UI** — New `src/stores/auth.ts` (Pinia) manages token + user, restores from `localStorage`, sets/clears the axios `Authorization` header. New `src/views/LoginView.vue` is a shadcn-vue card form. Router guard redirects unauthenticated users to `/login`. App.vue shows a logout button when authenticated.
- **EF Core baseline migration** — `DatabaseSeeder` now calls `MigrateAsync()`. Baseline `Initial` migration hand-authored in `src/rAspCoreVueLauncher.Api/Migrations/` covering all ASP.NET Identity tables (SQLite column types). `dotnet ef migrations add` failed on 10.0.8 due to a `MissingMethodException` in `AbstractionsStrings.ArgumentIsEmpty` — filed as a tooling limitation; migration written manually. 6/6 tests pass.

## 2026-05-27 — Handover docs + cleanup (`f702cb5` + post)

- Added `docs/HANDOVER.md`, `docs/CHANGELOG.md`, `docs/ROADMAP.md` — snapshot of current state, feature history, and recommended next sequence for the next session.
- Renamed the `API: Tests (debug)` launch config to `API: Run Tests` to reflect that it runs tests but does not support in-process breakpoints (use C# CodeLens "Debug Test" for that).
- Fixed a broken absolute-path wiki-link in `memory/feedback_use_subagents.md`.

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
