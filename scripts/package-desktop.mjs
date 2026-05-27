#!/usr/bin/env node

import { glob } from 'node:fs/promises';
import path from 'node:path';
import { run, webDir, header } from './lib/run.mjs';

async function listArtifacts(patterns) {
  const found = [];
  for (const pattern of patterns) {
    try {
      for await (const match of glob(pattern, { cwd: webDir })) {
        found.push(path.join(webDir, match));
      }
    } catch {
      // ignore missing dirs
    }
  }
  return found;
}

async function main() {
  if (process.platform === 'darwin') {
    console.error('macOS Tauri bundles require a Mac host.');
    process.exit(1);
  }

  header('web build (npm run build)');
  await run('npm', ['run', 'build'], { cwd: webDir });

  header('tauri build');
  await run('npm', ['run', 'tauri:build'], { cwd: webDir });

  header('artifacts');
  let patterns;
  if (process.platform === 'win32') {
    patterns = [
      'src-tauri/target/release/bundle/msi/*.msi',
      'src-tauri/target/release/bundle/nsis/*.exe',
    ];
  } else {
    patterns = [
      'src-tauri/target/release/bundle/deb/*.deb',
      'src-tauri/target/release/bundle/appimage/*.AppImage',
    ];
  }

  const artifacts = await listArtifacts(patterns);
  if (artifacts.length === 0) {
    console.log('(no bundle artifacts found — check tauri output above)');
  } else {
    for (const a of artifacts) console.log(a);
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
