#!/usr/bin/env node

import { spawn, spawnSync } from 'node:child_process';
import path from 'node:path';
import { repoRoot } from './lib/run.mjs';

function which(cmd) {
  const probeCmd = process.platform === 'win32' ? 'where' : 'which';
  const result = spawnSync(probeCmd, [cmd], { stdio: 'ignore' });
  return result.status === 0;
}

async function main() {
  const passthrough = process.argv.slice(2);
  let cmd;
  let args;

  if (process.platform === 'win32') {
    const script = path.join(repoRoot, 'scripts', 'setup.ps1');
    if (which('pwsh')) {
      cmd = 'pwsh';
    } else {
      cmd = 'powershell.exe';
    }
    args = ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', script, ...passthrough];
  } else {
    const script = path.join(repoRoot, 'scripts', 'setup.sh');
    cmd = 'bash';
    args = [script, ...passthrough];
  }

  const child = spawn(cmd, args, { stdio: 'inherit', cwd: repoRoot });
  child.on('error', (err) => {
    console.error(`Failed to spawn setup script: ${err.message}`);
    process.exit(1);
  });
  child.on('exit', (code, signal) => {
    if (signal) {
      console.error(`setup terminated by signal ${signal}`);
      process.exit(1);
    }
    process.exit(code ?? 1);
  });
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
