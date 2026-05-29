<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useHardwareStore } from '@/stores/hardware'
import { peekLatestLocalReading, type BridgeStatus } from '@/lib/sensorsBridge'
import type { MobileSensorReading } from '@/types/hardware'
import HardwareTestPage from '@/components/diagnostics/HardwareTestPage.vue'
import MobileReadingView from '@/components/diagnostics/MobileReadingView.vue'

const store = useHardwareStore()
const paused = ref(false)
const devices = computed(() => store.sensors?.mobileDevices ?? [])

const localReading = ref<MobileSensorReading | null>(null)
const localStatus = ref<BridgeStatus>('not-started')
let localTimer: number | undefined

function pollLocal() {
  const r = peekLatestLocalReading()
  // The bridge's MobileSensorReading is structurally compatible with the
  // hardware-types one (same field names); cast for display.
  localReading.value = r.reading as unknown as MobileSensorReading | null
  localStatus.value = r.status
}

onMounted(() => {
  store.startPolling()
  pollLocal()
  localTimer = window.setInterval(pollLocal, 1000)
})
onUnmounted(() => {
  store.stopPolling()
  if (localTimer != null) window.clearInterval(localTimer)
})

function refresh() { store.loadSensors() }
function togglePause() {
  if (paused.value) { store.startPolling(); paused.value = false }
  else { store.stopPolling(); paused.value = true }
}
</script>

<template>
  <HardwareTestPage
    title="Mobile / device sensors"
    endpoint="/api/hardware/sensors"
    :meta="store.sensorsMeta"
    :paused="paused"
    :loading="store.sensors === null && !store.error"
    :error="store.sensorsMeta.ok ? null : store.error"
    :raw-value="devices"
    @refresh="refresh"
    @toggle-pause="togglePause"
  >
    <div class="space-y-6">
      <div>
        <h2 class="mb-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          Cached devices ({{ devices.length }})
        </h2>
        <p v-if="devices.length === 0" class="text-sm text-muted-foreground">no devices reporting</p>
        <div v-for="(d, i) in devices" :key="d.clientId + ':' + i" class="mb-4 rounded border border-border/40 p-3">
          <MobileReadingView :reading="d" />
        </div>
      </div>

      <div class="border-t pt-4">
        <h2 class="mb-2 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          This client (local sensor bridge)
        </h2>
        <p class="mb-2 text-xs text-muted-foreground">status: {{ localStatus }}</p>
        <p v-if="!localReading" class="text-sm text-muted-foreground">no local reading captured yet</p>
        <div v-else class="rounded border border-border/40 p-3">
          <MobileReadingView :reading="localReading" />
        </div>
      </div>
    </div>
  </HardwareTestPage>
</template>
