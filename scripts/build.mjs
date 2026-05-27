#!/usr/bin/env node

import { run, repoRoot, webDir, header } from './lib/run.mjs';

async function main() {
  header('dotnet build');
  await run('dotnet', ['build', 'rAspCoreVueLauncher.slnx'], { cwd: repoRoot });

  header('web build (npm run build)');
  await run('npm', ['run', 'build'], { cwd: webDir });
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
