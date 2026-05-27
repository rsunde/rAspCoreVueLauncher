# Roadmap

What's next for `rAspCoreVueLauncher`. For the current state see [`HANDOVER.md`](HANDOVER.md); for shipped history see [`CHANGELOG.md`](CHANGELOG.md).

## Recommended next sequence

If you only have time for one path, do these in order. Each item unblocks the next.

### 1. Tauri sidecar wiring

Publish the ASP.NET API as a self-contained binary and reference it as a Tauri `externalBin` so the desktop bundle actually ships with a backend. Without this, `npm run package:desktop` produces a desktop app that has no API to talk to and `fetch('/api/...')` at `tauri://localhost` goes nowhere. The current [`README.md`](../README.md#packaging-notes) and [`BYO-APP.md`](BYO-APP.md#tauri-build-cant-reach-the-api) both flag this as a known gap.

Touch:
- `src/rAspCoreVueLauncher.Web/src-tauri/tauri.conf.json` — declare the sidecar under `bundle.externalBin` and `app.security` rules.
- `src/rAspCoreVueLauncher.Api/rAspCoreVueLauncher.Api.csproj` — single-file, self-contained `dotnet publish` config per RID.
- `scripts/package-desktop.mjs` — run `dotnet publish -r <rid>` before invoking Tauri and copy the binary into the location `tauri.conf.json` expects.
- Tauri Rust entry — spawn the sidecar on app start, kill on exit, pick a free local port and pass it to the webview as a window arg or env var.

### 2. Wire `startSensorBridge()` into the default Vue entry

The drop-in module already exists at `src/rAspCoreVueLauncher.Web/src/lib/sensorsBridge.ts` and `src/rAspCoreVueLauncher.Web/src/main.ts` is the entry. Add the import + call so the default app exercises the sensor pipeline end-to-end. iOS users will still need the gesture-gated pattern shown in [`BYO-APP.md`](BYO-APP.md#ios-permission-gotcha).

### 3. `VITE_API_BASE_URL` env var

Pull the API base URL out of hard-coded same-origin and into a Vite env var so mobile Capacitor builds can point at a remote API. Touch `src/rAspCoreVueLauncher.Web/src/api/client.ts` and the default arguments inside `sensorsBridge.ts`. Add a `.env.production` example committed at `src/rAspCoreVueLauncher.Web/.env.production.example`.

### 4. OpenAPI → TypeScript codegen

Replace the hand-written `src/rAspCoreVueLauncher.Web/src/types/hardware.ts` with a file generated from the API's `/openapi/v1.json` output. The file already carries an in-source TODO: `// In future, generate this from the API's OpenAPI document.` Use `openapi-typescript` driven from a small `scripts/generate-types.mjs`, and wire it into `npm run build` so it can't drift.

## Distribution gaps

Independent of the sequence above. Pick these up when you actually need to ship.

- **Code signing.** Windows Authenticode entries in `tauri.conf.json` (`bundle.windows.certificateThumbprint`, `digestAlgorithm`, `timestampUrl`). Android Gradle keystore + `signingConfigs` in `android/app/build.gradle`.
- **Tauri auto-update channel.** Updater endpoint, signing key, version manifest.
- **Production API base URL for mobile builds.** Covered by item 3 above; flagged here too because it gates any real mobile release.

## Half-finished / cleanup

Small, scoped items. Useful when you want to pay down without committing to a major feature.

- **Auth UI in Vue.** A login form against the seeded `dev@example.com / Dev!2345` user that stores the JWT and sets the axios `Authorization` header. The API side is complete.
- **EF Core migrations.** Replace the `EnsureCreated` call in `src/rAspCoreVueLauncher.Api/Program.cs` with `MigrateAsync` and add a baseline migration (`dotnet ef migrations add Initial`).
- **Fix or remove `API: Tests (debug)` launch config.** It runs `dotnet test` but doesn't actually break on test breakpoints; the C# extension's CodeLens "Debug Test" buttons are the supported path. Either remove the entry or rewrite it to use VSTest's debug-attach flow.
- **Stale link in user memory.** The user-memory file `feedback_use_subagents.md` contains a broken `[[../../../../X:/...]]` reference. Drop it.
- **`BatterySnapshot` is always `null`.** Could be filled on Windows via WMI (`Win32_Battery`) and on Linux via `/sys/class/power_supply/BAT*`. Skip on macOS until a host is available.
- **Per-client mobile-sensor history.** `MobileSensorCache` is single-field, latest-wins. Swap to `Dictionary<clientId, MobileSensorReading>` with a TTL eviction pass if multiple devices need to stream concurrently.
- **iOS support.** Capacitor iOS, Tauri macOS bundle. Requires a macOS host; explicitly out-of-scope on Windows/Linux dev hosts.

## Things explicitly punted

These were considered and deliberately not done. Don't pick them up without checking with the user first.

- **CI/CD (GitHub Actions matrix).** User said skip for now.
- **Auto-install in setup scripts.** User picked verify-only — the scripts print hints and exit.
- **`gh release create` + QR-code share modes in the wizard.** User picked "open artifact folder" only.
- **Wizard parallel-process orchestration for "Full stack".** `@clack/prompts` owns the TTY and fights long-lived child processes; the wizard prints a two-terminal recipe instead. Use the VS Code compound if you want one-keystroke startup.
