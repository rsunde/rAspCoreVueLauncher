#!/usr/bin/env node

import { glob, mkdir, copyFile } from 'node:fs/promises';
import path from 'node:path';
import { run, webDir, repoRoot, apiDir, header } from './lib/run.mjs';

// Maps Node.js platform → (dotnet RID, Tauri target triple, binary extension).
function getRidInfo() {
  if (process.platform === 'win32') {
    return { rid: 'win-x64', triple: 'x86_64-pc-windows-msvc', ext: '.exe' };
  }
  if (process.platform === 'linux') {
    return { rid: 'linux-x64', triple: 'x86_64-unknown-linux-gnu', ext: '' };
  }
  throw new Error(`Unsupported platform for desktop packaging: ${process.platform}`);
}

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

  const { rid, triple, ext } = getRidInfo();
  const publishOutDir = path.join(apiDir, 'publish', rid);
  const binariesDir = path.join(webDir, 'src-tauri', 'binaries');
  const publishedBinary = path.join(publishOutDir, `rAspCoreVueLauncher.Api${ext}`);
  const sidecarDest = path.join(binariesDir, `rAspCoreVueLauncher-api-${triple}${ext}`);

  // 1. Publish the ASP.NET API as a self-contained single binary for the target RID.
  header(`dotnet publish API (${rid})`);
  await run('dotnet', [
    'publish',
    path.join(apiDir, 'rAspCoreVueLauncher.Api.csproj'),
    '-r', rid,
    '--self-contained',
    '-c', 'Release',
    '-p:PublishSingleFile=true',
    '-p:DebugType=none',
    '-o', publishOutDir,
  ], { cwd: repoRoot });

  // 2. Copy the binary into src-tauri/binaries/ with the Tauri-expected naming.
  await mkdir(binariesDir, { recursive: true });
  await copyFile(publishedBinary, sidecarDest);
  console.log(`✓ Sidecar staged: ${sidecarDest}`);

  // 3. Build the Vite frontend with the production API URL baked in.
  header('web build (npm run build)');
  await run('npm', ['run', 'build'], {
    cwd: webDir,
    env: { VITE_API_BASE_URL: 'http://127.0.0.1:5202' },
  });

  // 4. Tauri build (beforeBuildCommand in tauri.conf.json would re-run npm build,
  //    but Tauri skips it when invoked via this script since we passed --no-bundle false
  //    and the build already ran). tauri:build invokes `tauri build` which runs the
  //    beforeBuildCommand again — set the env var so it keeps the right API URL.
  header('tauri build');
  await run('npm', ['run', 'tauri:build'], {
    cwd: webDir,
    env: { VITE_API_BASE_URL: 'http://127.0.0.1:5202' },
  });

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
