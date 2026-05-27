import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import { router } from './router'
import { startSensorBridge } from './lib/sensorsBridge'
import './style.css'

createApp(App)
  .use(createPinia())
  .use(router)
  .mount('#app')

// iOS: must be called from a user-gesture handler — see docs/BYO-APP.md#ios-permission-gotcha.
startSensorBridge({ apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? '' })
