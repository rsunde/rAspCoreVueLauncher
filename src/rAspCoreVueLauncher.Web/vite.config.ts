import { defineConfig } from 'vite'
import { fileURLToPath, URL } from 'node:url'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    // Per-clone override: set PORT=<n> in .env.local or your shell.
    // Keep src-tauri/tauri.conf.json:devUrl in sync if you change this default.
    port: Number(process.env.PORT) || 5172,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5202',
        changeOrigin: true,
      },
    },
  },
})
