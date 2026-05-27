import type { CapacitorConfig } from '@capacitor/cli'

const config: CapacitorConfig = {
  appId: 'io.github.rsunde.raspcorevuelauncher',
  appName: 'rAspCoreVueLauncher',
  webDir: 'dist',
  server: {
    androidScheme: 'https',
  },
}

export default config
