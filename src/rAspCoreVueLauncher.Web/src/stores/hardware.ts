import { defineStore } from 'pinia'
import { ref } from 'vue'
import { api } from '@/api/client'
import type { HardwareInfo, HardwareSensors, MobileSensorReading } from '@/types/hardware'

export const useHardwareStore = defineStore('hardware', () => {
  const info = ref<HardwareInfo | null>(null)
  const sensors = ref<HardwareSensors | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const sensorClockOffsetMs = ref(0) // (server - client) at last sensor fetch

  let pollHandle: number | null = null

  async function loadInfo() {
    loading.value = true
    error.value = null
    try {
      const { data } = await api.get<HardwareInfo>('/api/hardware/info')
      info.value = data
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Unknown error'
    } finally {
      loading.value = false
    }
  }

  async function loadSensors() {
    try {
      const { data } = await api.get<HardwareSensors>('/api/hardware/sensors')
      sensors.value = data
      sensorClockOffsetMs.value = new Date(data.serverTimeUtc).getTime() - Date.now()
      error.value = null
    } catch (e) {
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

  return { info, sensors, loading, error, sensorClockOffsetMs, loadInfo, loadSensors, startPolling, stopPolling }
})
