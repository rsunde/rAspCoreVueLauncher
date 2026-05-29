import { setLauncherToken } from '@/api/client'

// In a Tauri WebView, window.__TAURI_INTERNALS__ exists and the `fs_token`
// command returns the launcher token. In a plain browser (dev) it's absent, so
// fall back to VITE_FS_TOKEN (empty by default — dev API has no token configured).
export async function initLauncherToken(): Promise<void> {
  const isTauri = typeof window !== 'undefined' && '__TAURI_INTERNALS__' in window
  if (isTauri) {
    const { invoke } = await import('@tauri-apps/api/core')
    const token = await invoke<string>('fs_token')
    setLauncherToken(token)
    return
  }
  const devToken = import.meta.env.VITE_FS_TOKEN
  if (devToken) setLauncherToken(devToken)
}
