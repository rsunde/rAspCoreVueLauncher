# Agent guidance for rAspCoreVueLauncher

Notes for AI coding agents (Claude Code, etc.) working in this repo.

**Ports** (estate slot 2): Vite 5172 · API http 5202 · API https 7202. See estate `../AGENTS.md` → Port registry.

## Working style

- **Delegate aggressively to sub-agents.** When work is parallelizable, hand
  pieces to specialized agents (Explore for read-only search, Plan for design,
  general-purpose for self-contained slices of implementation). Keep the main
  conversation focused on orchestration and review.
- Run independent sub-agent calls in a single message so they execute in
  parallel.

## Dependencies

- **Keep everything on latest stable.** Don't let the stack drift. Whenever you
  work in the repo, check for outdated packages and bring them current as part of
  the task — both frontend (npm/pnpm in `src\rAspCoreVueLauncher.Web`: Vue, Vite,
  Tailwind, shadcn-vue, Pinia, Axios, Capacitor, Tauri, etc.) and backend (NuGet
  packages plus the .NET SDK). Use `pnpm outdated` and `dotnet list package
  --outdated`, update, then run the tests. Flag major-version bumps that need code
  changes and do that work — never pin to an old version to avoid it.

## Session budget

- **5-hour budget plan.** If a usage limit cuts the session off mid-task, call
  `ScheduleWakeup` with `delaySeconds ≈ 3600` and a prompt that resumes the
  current task. Keep going across wake-ups until the task is fully done — no
  need to re-ask the user each time.

## Naming

- Every `r*` project name (the `r` is intentional, lowercase) keeps the
  lowercase `r` everywhere: folder, `.csproj`, namespace, sln entry. Do not
  capitalise it.
