#!/usr/bin/env node

import { header } from './lib/run.mjs';

async function main() {
  if (process.platform !== 'darwin') {
    console.log('iOS/macOS builds require a macOS host (Xcode). Skipping.');
    process.exit(0);
  }

  header('ios build recipe (not executed)');
  console.log('Run the following on a macOS host with Xcode installed:');
  console.log('  npx cap add ios');
  console.log('  npx cap sync ios');
  console.log('  open ios/App/App.xcworkspace');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
