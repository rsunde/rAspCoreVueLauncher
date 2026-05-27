#!/usr/bin/env node

import fs from 'node:fs';
import { glob } from 'node:fs/promises';
import path from 'node:path';
import { run, webDir, header } from './lib/run.mjs';

async function main() {
  const sdk = process.env.ANDROID_HOME || process.env.ANDROID_SDK_ROOT;
  if (!sdk) {
    console.error('ANDROID_HOME or ANDROID_SDK_ROOT must be set to package the Android app.');
    process.exit(1);
  }

  const androidDir = path.join(webDir, 'android');
  if (!fs.existsSync(androidDir)) {
    header('cap add android (one-time bootstrap)');
    console.log('Note: this is a one-time bootstrap — the android/ folder will be committed afterward if desired.');
    await run('npx', ['cap', 'add', 'android'], { cwd: webDir });
  }

  header('web build (npm run build)');
  await run('npm', ['run', 'build'], { cwd: webDir });

  header('cap sync android');
  await run('npx', ['cap', 'sync', 'android'], { cwd: webDir });

  header('gradle assembleRelease');
  const gradleCmd = process.platform === 'win32' ? 'gradlew.bat' : './gradlew';
  await run(gradleCmd, ['assembleRelease'], { cwd: androidDir });

  header('artifacts');
  const pattern = 'app/build/outputs/apk/release/*.apk';
  let found = false;
  for await (const match of glob(pattern, { cwd: androidDir })) {
    console.log(path.join(androidDir, match));
    found = true;
  }
  if (!found) {
    console.log('(no APK found — check gradle output above)');
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
