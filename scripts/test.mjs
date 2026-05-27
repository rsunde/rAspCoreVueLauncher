#!/usr/bin/env node

import { run, repoRoot, header } from './lib/run.mjs';

async function main() {
  header('dotnet test');
  await run('dotnet', ['test', 'rAspCoreVueLauncher.slnx', '--nologo'], { cwd: repoRoot });
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
