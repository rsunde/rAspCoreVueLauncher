import axios from 'axios'

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '/',
  headers: { 'Content-Type': 'application/json' },
})

export function setLauncherToken(token: string): void {
  api.defaults.headers.common['X-Launcher-Token'] = token
}
