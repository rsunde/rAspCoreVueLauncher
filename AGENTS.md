# AGENTS.md

Single source of truth for **every** AI coding agent on this repository — Claude Code, OpenAI
Codex, Cursor, GitHub Copilot, Gemini CLI, Windsurf, Aider, etc. Edit **this** file; the
tool-specific files (`CLAUDE.md`, `GEMINI.md`, `.cursor/rules/`, `.github/copilot-instructions.md`,
`.windsurfrules`) are thin pointers that import or defer to it. Keep it concise — instructions,
not full docs.

---

## 1. Project Overview

- **rAspCoreVueLauncher** — a cross-platform app *template/launcher*: an ASP.NET Core minimal API
  exposing device hardware/sensor/filesystem access, paired with a Vue 3 SPA, packaged for
  desktop (Tauri) and mobile (Capacitor). Drop your own Vue app on top and get sensor/filesystem
  access without writing native plugin code per-platform. It is **not** your app's backend —
  auth/business logic stays in your own app's separate API. See `docs/BYO-APP.md`.
- **Stack**: .NET 10 SDK (`10.0.300` pinned in `global.json`, `rollForward: latestMinor`),
  ASP.NET Core 10 minimal API. Frontend: Vue `^3.5` + Vite `^8` + TypeScript `~6.0`, Tailwind v4
  (`@tailwindcss/vite`), shadcn-vue (via `reka-ui`, `components.json`), Pinia, Axios, Vue Router,
  Tauri 2 (desktop shell), Capacitor (mobile shell). Tests: MSTest + NSubstitute +
  FluentAssertions + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`).
- **Layout**:
  - `src/rAspCoreVueLauncher.Shared/` — .NET class library, DTOs only (`Hardware/`,
    `Filesystem/`, `Auth/`, `Seed/`).
  - `src/rAspCoreVueLauncher.Api/` — ASP.NET minimal API: `Hardware/` (info/sensors/mobile-sensor
    endpoints) and `Filesystem/` (list/roots/read/download/write/mkdir/move/copy/delete, plus
    platform-specific trash: `WindowsFileTrash.cs`, `LinuxFileTrash.cs`, `NullFileTrash.cs`, and
    `LauncherSecurity.cs`).
  - `src/rAspCoreVueLauncher.Web/` — Vue 3 app; `src-tauri/` (Rust desktop shell);
    `capacitor.config.ts` (mobile — no native `android/`/`ios/` folders committed).
  - `tests/rAspCoreVueLauncher.Api.Tests/` — MSTest project, 4 files, ~28 `[TestMethod]`s
    covering Hardware + Filesystem endpoints/service/security. Real, not stubbed.
  - `scripts/` — Node orchestrators (`build.mjs`, `test.mjs`, `clean.mjs`, `package-*.mjs`,
    `wizard.mjs` using `@clack/prompts`) that the root `package.json` scripts dispatch to, plus
    `setup.ps1`/`setup.sh` (verify-only host checks, no installs).
  - `docs/` — `BYO-APP.md`, `CHANGELOG.md`, `HANDOVER.md`, `ROADMAP.md`, and
    `docs/superpowers/{plans,specs}/` for dated design docs.

---

## 2. Commands

```bash
# Dev (two terminals — no combined dev launcher exists yet)
just dev-api    # dotnet watch --project src/rAspCoreVueLauncher.Api/rAspCoreVueLauncher.Api.csproj run --launch-profile https
just dev-web    # npm --prefix src/rAspCoreVueLauncher.Web run dev

# Build everything (backend + frontend, via scripts/build.mjs)
just build      # == npm run build

# Test everything (via scripts/test.mjs)
just test       # == npm run test

# .NET-only build/test (solution is rAspCoreVueLauncher.slnx, the newer XML format — dotnet CLI
# on the .NET 10 SDK reads it directly)
dotnet build rAspCoreVueLauncher.slnx
dotnet test tests/rAspCoreVueLauncher.Api.Tests/rAspCoreVueLauncher.Api.Tests.csproj
```

Dev ports (estate slot 2 — see `X:\git\rsunde\AGENTS.md` port registry): Vite `5172`, API http
`5202`, API https `7202`. Vite's dev proxy forwards `/api/*` to `http://localhost:5202`
(`vite.config.ts`). **Note**: README.md states port 5173 in one spot, but `vite.config.ts`'s
actual configured default is 5172 — trust the config, not the README, until reconciled.

---

## 3. Architecture

Two independent processes in dev: the ASP.NET API (`https://localhost:7202` /
`http://localhost:5202`) and the Vite dev server (`http://localhost:5172`), with Vite proxying
`/api/*` to the API. In production/Tauri builds, the API is meant to run as a local sidecar
reachable same-origin via Axios (`src/api/client.ts`) — see §7, this wiring isn't finished yet.
CORS policy `VueDevCors` in `Program.cs` accepts any `localhost:*` origin for dev. A typical
hardware/filesystem feature change touches four layers in order: a DTO in `Shared/`, an endpoint
in `Api/Hardware|Filesystem/`, the matching Pinia store (`Web/src/stores/`), and the
view/component (`Web/src/views|components/`) — e.g. the existing `FileManagerPanel.vue` feature
follows this exact path end-to-end and is a good reference.

---

## 4. Conventions

- Write clean, modern, readable code. Prioritise readability and maintainability.
- Explain complex logic or significant architectural decisions.
- Standard .NET/C# conventions on the API side; keep projects under `src/`. Standard Vue 3 +
  TypeScript conventions on the frontend; components use shadcn-vue primitives via `reka-ui`
  where a pattern already exists rather than hand-rolling new ones.
- Every `r*` project name (the `r` is intentional, lowercase) keeps the lowercase `r` everywhere:
  folder, `.csproj`, namespace, solution entry. Do not capitalise it.
- **Keep dependencies current.** Check for outdated packages as part of any task touching them —
  both frontend (`npm outdated` under `src/rAspCoreVueLauncher.Web`) and backend (`dotnet list
  package --outdated`). Update and re-run tests rather than pinning to an old version to dodge a
  bump; flag major-version bumps that need code changes and do that work.

---

## 5. Rules (non-negotiable — these override default agent behaviour)

1. **Source of Truth**: this `AGENTS.md` is the single source of truth. Make all changes here,
   never in the pointer files.
2. **No root clutter**: don't create temporary files in the repo root; clean up after yourself.
3. **Safety**: never delete data or implementation files (or delete markdown content) without
   explicit confirmation. Prefer moving superseded files aside over deleting them.
4. **Delegate to sub-agents** for any multi-step or multi-file work. Reserve the main thread for
   orchestration — planning, dispatching, summarising. The conductor, not the player.

---

## 6. Testing & Definition of Done

A feature isn't done when it compiles — it's done when it builds, tests pass, and the
docs/README reflect it.

- Run tests with: `just test` (== `npm run test`, dispatches to `scripts/test.mjs`, covers the
  MSTest suite in `tests/rAspCoreVueLauncher.Api.Tests/`).
- Before claiming done: build clean (`just build`), tests green, no new root-level clutter.

---

## 7. Do-not-touch / gotchas

- **`docs/HANDOVER.md` is stale.** It claims "13 tests" (actually ~28) and its file tree/prose
  never mention the Filesystem API/service/security/trash code or `FileManagerPanel.vue`, even
  though that's a fully built feature with its own design doc
  (`docs/superpowers/specs/2026-05-29-filesystem-file-manager-design.md`). `README.md` also
  never mentions the filesystem endpoints at all. Don't trust either doc as a complete feature
  inventory — check `src/` directly.
- `docs/HANDOVER.md` references a root `AGENTS.md` and a Claude memory path that didn't exist in
  this repo before this task — those references were aspirational/stale; this file is what makes
  them true now.
- **Known intentional gaps** (per `docs/ROADMAP.md` / `docs/HANDOVER.md`, not bugs to silently
  fix): `BatterySnapshot` is hard-coded `null` server-side; the Tauri sidecar isn't wired yet (the
  API isn't bundled with the desktop binary); Android/iOS native folders are intentionally not
  committed; there is no CI/CD (explicitly punted); `sensorsBridge.ts` doesn't populate
  health/biometric sensor fields.
- Generated/vendored — don't touch or commit into: `node_modules/` (root and `Web/`),
  `src/*/bin/`, `src/*/obj/`, `src/rAspCoreVueLauncher.Web/dist/`.
- `AGENTS_ronnie.md` (repo root) is a personal/estate-specific notes file predating this
  standardization (ports, dependency-freshness policy, naming convention, session-budget
  behavior) — its content has been folded into this `AGENTS.md` where relevant; treat this file,
  not that one, as canonical going forward.
