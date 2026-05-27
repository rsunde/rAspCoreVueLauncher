#!/usr/bin/env node
// Generates src/types/api.gen.ts from the running API's OpenAPI spec.
// The API must be running before this script is invoked.
// Start it: dotnet run --project src/rAspCoreVueLauncher.Api
// Usage:    npm run gen:types

import { execSync } from 'node:child_process'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const webDir = join(__dirname, '..', 'src', 'rAspCoreVueLauncher.Web')
const schemaUrl = 'http://localhost:5148/openapi/v1.json'
const outputFile = 'src/types/api.gen.ts'

console.log(`Fetching schema from ${schemaUrl} ...`)
try {
  execSync(`npx openapi-typescript "${schemaUrl}" -o "${outputFile}"`, {
    cwd: webDir,
    stdio: 'inherit',
  })
  console.log(`✓ Generated ${outputFile}`)
  console.log('  Import from api.gen.ts instead of hand-editing hardware.ts when types drift.')
} catch {
  console.error(`✗ Failed — is the API running at ${schemaUrl}?`)
  console.error('  Start it with: dotnet run --project src/rAspCoreVueLauncher.Api')
  process.exit(1)
}
