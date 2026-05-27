# Roadmap

What's next for `rAspCoreVueLauncher`. For the current state see [`HANDOVER.md`](HANDOVER.md); for shipped history see [`CHANGELOG.md`](CHANGELOG.md).

## Recommended next sequence

All four original sequence items are shipped. The next natural feature work is below.

## Distribution gaps

Independent of the sequence above. Pick these up when you actually need to ship.

- **Code signing.** Windows Authenticode entries in `tauri.conf.json` (`bundle.windows.certificateThumbprint`, `digestAlgorithm`, `timestampUrl`). Android Gradle keystore + `signingConfigs` in `android/app/build.gradle`.
- **Tauri auto-update channel.** Updater endpoint, signing key, version manifest.
- **Production API base URL for mobile builds.** `VITE_API_BASE_URL` is wired. Set it at build time to your production API host for mobile Capacitor deployments.

## Half-finished / cleanup

Small, scoped items. Useful when you want to pay down without committing to a major feature.

- **`BatterySnapshot` is always `null`.** Could be filled on Windows via WMI (`Win32_Battery`) and on Linux via `/sys/class/power_supply/BAT*`. Skip on macOS until a host is available.
- **Per-client mobile-sensor history.** `MobileSensorCache` is single-field, latest-wins. Swap to `Dictionary<clientId, MobileSensorReading>` with a TTL eviction pass if multiple devices need to stream concurrently.
- **iOS support.** Capacitor iOS, Tauri macOS bundle. Requires a macOS host; explicitly out-of-scope on Windows/Linux dev hosts.

## Things explicitly punted

These were considered and deliberately not done. Don't pick them up without checking with the user first.

- **CI/CD (GitHub Actions matrix).** User said skip for now.
- **Auto-install in setup scripts.** User picked verify-only — the scripts print hints and exit.
- **`gh release create` + QR-code share modes in the wizard.** User picked "open artifact folder" only.
- **Wizard parallel-process orchestration for "Full stack".** `@clack/prompts` owns the TTY and fights long-lived child processes; the wizard prints a two-terminal recipe instead. Use the VS Code compound if you want one-keystroke startup.
