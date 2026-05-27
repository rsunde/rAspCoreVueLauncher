# Agent guidance for rAspCoreVueLauncher

Notes for AI coding agents (Claude Code, etc.) working in this repo.

## Working style

- **Delegate aggressively to sub-agents.** When work is parallelizable, hand
  pieces to specialized agents (Explore for read-only search, Plan for design,
  general-purpose for self-contained slices of implementation). Keep the main
  conversation focused on orchestration and review.
- Run independent sub-agent calls in a single message so they execute in
  parallel.

## Session budget

- **5-hour budget plan.** If a usage limit cuts the session off mid-task, call
  `ScheduleWakeup` with `delaySeconds ≈ 3600` and a prompt that resumes the
  current task. Keep going across wake-ups until the task is fully done — no
  need to re-ask the user each time.

## Naming

- Every `r*` project name (the `r` is intentional, lowercase) keeps the
  lowercase `r` everywhere: folder, `.csproj`, namespace, sln entry. Do not
  capitalise it.
