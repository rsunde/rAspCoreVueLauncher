#!/usr/bin/env node

import {
  intro,
  outro,
  select,
  confirm,
  spinner,
  note,
  cancel,
  isCancel,
} from '@clack/prompts';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { run, repoRoot, webDir } from './lib/run.mjs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

function bail(answer) {
  if (isCancel(answer)) {
    cancel('Cancelled');
    process.exit(0);
  }
  return answer;
}

async function safeRun(label, fn) {
  try {
    await fn();
  } catch (err) {
    note(err.message, `${label} failed`);
  }
}

function openFolder(p) {
  if (process.platform === 'win32') return run('explorer', [p]).catch(() => {});
  if (process.platform === 'darwin') return run('open', [p]);
  return run('xdg-open', [p]);
}

function globFiles(dir, exts) {
  if (!fs.existsSync(dir)) return [];
  const out = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (!entry.isFile()) continue;
    const lower = entry.name.toLowerCase();
    if (exts.some((e) => lower.endsWith(e))) {
      out.push(path.join(dir, entry.name));
    }
  }
  return out;
}

function desktopBundleDirs() {
  const base = path.join(webDir, 'src-tauri', 'target', 'release', 'bundle');
  if (process.platform === 'win32') {
    return [
      { dir: path.join(base, 'msi'), exts: ['.msi'] },
      { dir: path.join(base, 'nsis'), exts: ['.exe'] },
    ];
  }
  if (process.platform === 'darwin') {
    return [
      { dir: path.join(base, 'macos'), exts: ['.app'] },
      { dir: path.join(base, 'dmg'), exts: ['.dmg'] },
    ];
  }
  return [
    { dir: path.join(base, 'deb'), exts: ['.deb'] },
    { dir: path.join(base, 'appimage'), exts: ['.appimage'] },
  ];
}

function androidArtifactDirs() {
  const base = path.join(webDir, 'android', 'app', 'build', 'outputs');
  return [
    { dir: path.join(base, 'apk', 'release'), exts: ['.apk'] },
    { dir: path.join(base, 'bundle', 'release'), exts: ['.aab'] },
  ];
}

async function revealArtifacts(label, groups) {
  const s = spinner();
  s.start('Discovering artifacts');
  const found = [];
  for (const g of groups) {
    for (const f of globFiles(g.dir, g.exts)) found.push(f);
  }
  s.stop('Artifact scan complete');
  if (found.length === 0) {
    note('No artifacts found in expected locations.', label);
    return;
  }
  note(found.join('\n'), 'Artifacts');
  const opened = new Set();
  for (const g of groups) {
    if (!fs.existsSync(g.dir)) continue;
    if (opened.has(g.dir)) continue;
    opened.add(g.dir);
    await openFolder(g.dir);
  }
}

async function devMenu() {
  const choice = bail(
    await select({
      message: 'Pick a dev environment',
      options: [
        { value: 'api-http', label: 'API only (HTTP)' },
        { value: 'api-https', label: 'API only (HTTPS)' },
        { value: 'web', label: 'Web only (Vite)' },
        { value: 'desktop', label: 'Desktop (Tauri)' },
        { value: 'android', label: 'Mobile (Capacitor Android)' },
        { value: 'ios', label: 'Mobile (Capacitor iOS)' },
        { value: 'full', label: 'Full stack (API + Vue)' },
        { value: 'back', label: 'Back' },
      ],
    }),
  );
  if (choice === 'back') return;
  if (choice === 'api-http') {
    await safeRun('API (HTTP)', () =>
      run('dotnet', [
        'watch',
        '--project',
        'src/rAspCoreVueLauncher.Api',
        'run',
        '--launch-profile',
        'http',
      ]),
    );
    return;
  }
  if (choice === 'api-https') {
    await safeRun('API (HTTPS)', () =>
      run('dotnet', [
        'watch',
        '--project',
        'src/rAspCoreVueLauncher.Api',
        'run',
        '--launch-profile',
        'https',
      ]),
    );
    return;
  }
  if (choice === 'web') {
    await safeRun('Web dev', () => run('npm', ['run', 'dev'], { cwd: webDir }));
    return;
  }
  if (choice === 'desktop') {
    await safeRun('Tauri dev', () => run('npm', ['run', 'tauri:dev'], { cwd: webDir }));
    return;
  }
  if (choice === 'android') {
    if (!process.env.ANDROID_HOME) {
      const proceed = bail(
        await confirm({
          message: 'ANDROID_HOME is not set. Continue anyway?',
          initialValue: false,
        }),
      );
      if (!proceed) return;
    }
    await safeRun('Capacitor Android', () =>
      run('npm', ['run', 'cap:android'], { cwd: webDir }),
    );
    return;
  }
  if (choice === 'ios') {
    if (process.platform !== 'darwin') {
      note('Capacitor iOS requires macOS. Skipping.', 'Unsupported host');
      return;
    }
    await safeRun('Capacitor iOS', () => run('npm', ['run', 'cap:ios'], { cwd: webDir }));
    return;
  }
  if (choice === 'full') {
    note(
      [
        'Running two long-lived processes from a single wizard is awkward.',
        '',
        'Recommended: VS Code compound launch "Full Stack: API (HTTP) + Vue".',
        '',
        'Or run these in two separate terminals:',
        '  Terminal 1: dotnet watch --project src/rAspCoreVueLauncher.Api run --launch-profile http',
        '  Terminal 2: npm run dev   (inside src/rAspCoreVueLauncher.Web)',
      ].join('\n'),
      'Full stack',
    );
  }
}

async function buildMenu() {
  const choice = bail(
    await select({
      message: 'Pick a build target',
      options: [
        { value: 'all', label: 'Build everything (API + Web)' },
        { value: 'api', label: 'API only' },
        { value: 'web', label: 'Web only' },
        { value: 'test', label: 'Run tests' },
        { value: 'back', label: 'Back' },
      ],
    }),
  );
  if (choice === 'back') return;
  if (choice === 'all') {
    await safeRun('Build', () => run('node', ['scripts/build.mjs']));
    return;
  }
  if (choice === 'api') {
    await safeRun('dotnet build', () => run('dotnet', ['build', 'rAspCoreVueLauncher.slnx']));
    return;
  }
  if (choice === 'web') {
    await safeRun('Web build', () => run('npm', ['run', 'build'], { cwd: webDir }));
    return;
  }
  if (choice === 'test') {
    await safeRun('Tests', () => run('node', ['scripts/test.mjs']));
  }
}

async function packageMenu() {
  const choice = bail(
    await select({
      message: 'Pick a package target',
      options: [
        { value: 'desktop', label: 'Desktop bundle (current host)' },
        { value: 'android', label: 'Android APK' },
        { value: 'all', label: 'Everything available on this host' },
        { value: 'back', label: 'Back' },
      ],
    }),
  );
  if (choice === 'back') return;
  if (choice === 'desktop') {
    let ok = true;
    await safeRun('Package desktop', async () => {
      try {
        await run('node', ['scripts/package-desktop.mjs']);
      } catch (e) {
        ok = false;
        throw e;
      }
    });
    if (ok) await revealArtifacts('Desktop', desktopBundleDirs());
    return;
  }
  if (choice === 'android') {
    if (!process.env.ANDROID_HOME) {
      const proceed = bail(
        await confirm({
          message: 'ANDROID_HOME is not set. Continue anyway?',
          initialValue: false,
        }),
      );
      if (!proceed) return;
    }
    let ok = true;
    await safeRun('Package Android', async () => {
      try {
        await run('node', ['scripts/package-android.mjs']);
      } catch (e) {
        ok = false;
        throw e;
      }
    });
    if (ok) await revealArtifacts('Android', androidArtifactDirs());
    return;
  }
  if (choice === 'all') {
    let ok = true;
    await safeRun('Package all', async () => {
      try {
        await run('node', ['scripts/package-all.mjs']);
      } catch (e) {
        ok = false;
        throw e;
      }
    });
    if (ok) {
      await revealArtifacts('Desktop', desktopBundleDirs());
      await revealArtifacts('Android', androidArtifactDirs());
    }
  }
}

async function setupMenu() {
  await safeRun('Setup check', () => run('node', ['scripts/setup-dispatch.mjs']));
}

async function cleanMenu() {
  const proceed = bail(
    await confirm({
      message: 'This will delete build outputs (bin, obj, dist, target, build). Continue?',
      initialValue: false,
    }),
  );
  if (!proceed) return;
  const deep = bail(
    await confirm({
      message: 'Also wipe node_modules (deep clean)?',
      initialValue: false,
    }),
  );
  const args = ['scripts/clean.mjs'];
  if (deep) args.push('--deep');
  await safeRun('Clean', () => run('node', args));
}

async function main() {
  intro('rAspCoreVueLauncher · wizard');
  while (true) {
    const action = bail(
      await select({
        message: 'What do you want to do?',
        options: [
          { value: 'dev', label: 'Run a dev environment' },
          { value: 'build', label: 'Build (no packaging)' },
          { value: 'package', label: 'Package for sharing' },
          { value: 'setup', label: 'Run setup check' },
          { value: 'clean', label: 'Clean build outputs' },
          { value: 'quit', label: 'Quit' },
        ],
      }),
    );
    if (action === 'quit') break;
    if (action === 'dev') await devMenu();
    else if (action === 'build') await buildMenu();
    else if (action === 'package') await packageMenu();
    else if (action === 'setup') await setupMenu();
    else if (action === 'clean') await cleanMenu();
  }
  outro('Goodbye');
}

main().catch((err) => {
  cancel(err?.message || String(err));
  process.exit(1);
});
