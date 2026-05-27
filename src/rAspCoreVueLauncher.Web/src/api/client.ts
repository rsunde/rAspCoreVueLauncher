import axios from 'axios'

// Same-origin in dev (Vite proxies /api to the ASP.NET backend),
// same-origin in Tauri (Tauri serves the SPA from tauri://localhost).
export const api = axios.create({
  baseURL: '/',
  headers: { 'Content-Type': 'application/json' },
})
