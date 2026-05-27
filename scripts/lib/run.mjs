#!/usr/bin/env node

import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

export const repoRoot = path.resolve(__dirname, '..', '..');
export const webDir = path.join(repoRoot, 'src', 'rAspCoreVueLauncher.Web');
export const apiDir = path.join(repoRoot, 'src', 'rAspCoreVueLauncher.Api');
export const testsDir = path.join(repoRoot, 'tests', 'rAspCoreVueLauncher.Api.Tests');

const NPM_LIKE = new Set(['npm', 'npx', 'cap', 'tauri', 'yarn', 'pnpm']);

function resolveCmd(cmd) {
  if (process.platform === 'win32' && NPM_LIKE.has(cmd)) {
    return `${cmd}.cmd`;
  }
  return cmd;
}

export function run(cmd, args = [], opts = {}) {
  const resolved = resolveCmd(cmd);
  const cwd = opts.cwd || repoRoot;
  const env = opts.env ? { ...process.env, ...opts.env } : process.env;

  return new Promise((resolve, reject) => {
    const child = spawn(resolved, args, {
      stdio: 'inherit',
      cwd,
      env,
      shell: false,
    });

    child.on('error', (err) => {
      reject(new Error(`Failed to spawn '${resolved} ${args.join(' ')}': ${err.message}`));
    });

    child.on('exit', (code, signal) => {
      if (code === 0) {
        resolve();
      } else if (signal) {
        reject(new Error(`'${resolved} ${args.join(' ')}' terminated by signal ${signal}`));
      } else {
        reject(new Error(`'${resolved} ${args.join(' ')}' exited with code ${code}`));
      }
    });
  });
}

export function header(label) {
  const useColor = process.stdout.isTTY;
  const banner = `\n${'─'.repeat(2)} ${label} ${'─'.repeat(2)}\n`;
  if (useColor) {
    process.stdout.write(`\x1b[1;36m${banner}\x1b[0m`);
  } else {
    process.stdout.write(banner);
  }
}
