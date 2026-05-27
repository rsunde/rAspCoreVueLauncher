#!/usr/bin/env node

import fs from 'node:fs';
import fsp from 'node:fs/promises';
import path from 'node:path';
import { repoRoot, webDir, header } from './lib/run.mjs';

async function removePath(p) {
  if (!fs.existsSync(p)) return false;
  await fsp.rm(p, { recursive: true, force: true });
  console.log(`removed: ${p}`);
  return true;
}

async function findDotnetArtifactDirs(roots) {
  const targets = [];
  for (const root of roots) {
    if (!fs.existsSync(root)) continue;
    const stack = [root];
    while (stack.length) {
      const dir = stack.pop();
      let entries;
      try {
        entries = await fsp.readdir(dir, { withFileTypes: true });
      } catch {
        continue;
      }
      for (const ent of entries) {
        if (!ent.isDirectory()) continue;
        const full = path.join(dir, ent.name);
        if (ent.name === 'node_modules') continue;
        if (ent.name === 'bin' || ent.name === 'obj') {
          targets.push(full);
          continue;
        }
        stack.push(full);
      }
    }
  }
  return targets;
}

async function main() {
  const deep = process.argv.includes('--deep');

  header('clean: .NET bin/obj');
  const dotnetDirs = await findDotnetArtifactDirs([
    path.join(repoRoot, 'src'),
    path.join(repoRoot, 'tests'),
  ]);
  for (const d of dotnetDirs) await removePath(d);

  header('clean: web artifacts');
  await removePath(path.join(webDir, 'dist'));
  await removePath(path.join(webDir, 'src-tauri', 'target'));
  await removePath(path.join(webDir, 'android', 'app', 'build'));

  if (deep) {
    header('clean: node_modules (--deep)');
    await removePath(path.join(webDir, 'node_modules'));
  } else {
    console.log('(skipping node_modules — pass --deep to also remove it)');
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
