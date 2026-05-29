import { defineStore } from 'pinia'
import { ref } from 'vue'
import { api } from '@/api/client'
import type { HardwareInfo, HardwareSensors, MobileSensorReading } from '@/types/hardware'

export interface FetchMeta {
  fetchedAt: number | null   // Date.now() when the fetch completed
  durationMs: number | null
  httpStatus: number | null
  ok: boolean
}

function emptyMeta(): FetchMeta {
  return { fetchedAt: null, durationMs: null, httpStatus: null, ok: false }
}

export const useHardwareStore = defineStore('hardware', () => {
  const info = ref<HardwareInfo | null>(null)
  const sensors = ref<HardwareSensors | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const sensorClockOffsetMs = ref(0) // (server - client) at last sensor fetch
  const infoMeta = ref<FetchMeta>(emptyMeta())
  const sensorsMeta = ref<FetchMeta>(emptyMeta())

  let pollHandle: number | null = null

  function statusOf(e: unknown): number | null {
    return (e as { response?: { status?: number } }).response?.status ?? null
  }

  async function loadInfo() {
    loading.value = true
    error.value = null
    const started = performance.now()
    try {
      const res = await api.get<HardwareInfo>('/api/hardware/info')
      info.value = res.data
      infoMeta.value = { fetchedAt: Date.now(), durationMs: Math.round(performance.now() - started), httpStatus: res.status, ok: true }
    } catch (e) {
      infoMeta.value = { fetchedAt: Date.now(), durationMs: Math.round(performance.now() - started), httpStatus: statusOf(e), ok: false }
      error.value = e instanceof Error ? e.message : 'Unknown error'
    } finally {
      loading.value = false
    }
  }

  async function loadSensors() {
    const started = performance.now()
    try {
      const res = await api.get<HardwareSensors>('/api/hardware/sensors')
      sensors.value = res.data
      sensorClockOffsetMs.value = new Date(res.data.serverTimeUtc).getTime() - Date.now()
      sensorsMeta.value = { fetchedAt: Date.now(), durationMs: Math.round(performance.now() - started), httpStatus: res.status, ok: true }
      error.value = null
    } catch (e) {
      sensorsMeta.value = { fetchedAt: Date.now(), durationMs: Math.round(performance.now() - started), httpStatus: statusOf(e), ok: false }
      error.value = e instanceof Error ? e.message : 'Unknown error'
    }
  }

  async function postMobileSensors(reading: MobileSensorReading) {
    await api.post('/api/hardware/sensors/mobile', reading)
  }

  function startPolling(intervalMs = 2000) {
    if (pollHandle !== null) return
    loadSensors()
    pollHandle = window.setInterval(loadSensors, intervalMs)
  }

  function stopPolling() {
    if (pollHandle !== null) {
      window.clearInterval(pollHandle)
      pollHandle = null
    }
  }

  return { info, sensors, loading, error, sensorClockOffsetMs, infoMeta, sensorsMeta, loadInfo, loadSensors, postMobileSensors, startPolling, stopPolling }
})
