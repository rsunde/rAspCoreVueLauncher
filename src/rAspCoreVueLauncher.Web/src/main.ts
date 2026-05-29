import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import { router } from './router'
import { startSensorBridge } from './lib/sensorsBridge'
import { initLauncherToken } from './launcherToken'
import './style.css'

const app = createApp(App)
  .use(createPinia())
  .use(router)

// Fetch the launcher token (Tauri) before mounting so the first filesystem
// request carries the X-Launcher-Token header.
initLauncherToken().finally(() => app.mount('#app'))

// iOS: must be called from a user-gesture handler — see docs/BYO-APP.md#ios-permission-gotcha.
startSensorBridge({ apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? '' })
