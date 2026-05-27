#!/usr/bin/env node

import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { run, repoRoot, webDir, header } from './lib/run.mjs';

const __filename = fileURLToPath(import.meta.url);
const scriptsDir = path.dirname(__filename);

async function runNodeScript(script) {
  await run(process.execPath, [path.join(scriptsDir, script)], { cwd: repoRoot });
}

async function main() {
  const produced = [];

  header('phase 1: api tests');
  await run('dotnet', ['test', 'rAspCoreVueLauncher.slnx', '--nologo'], { cwd: repoRoot });
  produced.push(['api tests', 'passed']);

  header('phase 2: web build');
  await run('npm', ['run', 'build'], { cwd: webDir });
  produced.push(['web dist', path.join(webDir, 'dist')]);

  header('phase 3: desktop bundle');
  await runNodeScript('package-desktop.mjs');
  produced.push(['desktop bundle', 'see package-desktop output']);

  if (process.env.ANDROID_HOME || process.env.ANDROID_SDK_ROOT) {
    header('phase 4: android');
    await runNodeScript('package-android.mjs');
    produced.push(['android apk', 'see package-android output']);
  } else {
    header('phase 4: android (skipped — no ANDROID_HOME)');
    produced.push(['android apk', 'skipped (ANDROID_HOME not set)']);
  }

  header('phase 5: ios');
  await runNodeScript('package-ios.mjs');
  produced.push(['ios', process.platform === 'darwin' ? 'recipe printed' : 'skipped (not macOS)']);

  header('summary');
  const widthA = Math.max(...produced.map(([a]) => a.length));
  for (const [label, value] of produced) {
    console.log(`  ${label.padEnd(widthA)}  ${value}`);
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
