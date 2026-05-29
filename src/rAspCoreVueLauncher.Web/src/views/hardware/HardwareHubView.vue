<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { useHardwareStore } from '@/stores/hardware'
import HardwareTestPage from '@/components/diagnostics/HardwareTestPage.vue'

const store = useHardwareStore()
const paused = ref(false)
onMounted(() => { if (!store.info) store.loadInfo(); store.startPolling() })
onUnmounted(() => store.stopPolling())
function refresh() { store.loadInfo(); store.loadSensors() }
function togglePause() {
  if (paused.value) { store.startPolling(); paused.value = false }
  else { store.stopPolling(); paused.value = true }
}

const s = computed(() => store.sensors)
const links = computed(() => [
  { to: '/hardware/info', label: 'Machine info', summary: store.info ? store.info.machineName : '—' },
  { to: '/hardware/cpu', label: 'CPU', summary: s.value ? `${s.value.cpu.processUsagePercent}%` : '—' },
  { to: '/hardware/memory', label: 'Memory', summary: s.value ? `${s.value.memory.processWorkingSetMb} MB` : '—' },
  { to: '/hardware/disks', label: 'Disks', summary: s.value ? `${s.value.disks.length}` : '—' },
  { to: '/hardware/networks', label: 'Networks', summary: s.value ? `${s.value.networks.length}` : '—' },
  { to: '/hardware/battery', label: 'Battery', summary: s.value ? (s.value.battery ? `${s.value.battery.percentRemaining}%` : 'none') : '—' },
  { to: '/hardware/mobile', label: 'Mobile / device sensors', summary: s.value ? `${s.value.mobileDevices.length} device(s)` : '—' },
])
</script>

<template>
  <HardwareTestPage
    title="Hardware diagnostics"
    endpoint="/api/hardware/info + /sensors"
    :meta="store.sensorsMeta"
    :paused="paused"
    :loading="store.sensors === null && !store.error"
    :error="(store.infoMeta.ok && store.sensorsMeta.ok) ? null : store.error"
    :raw-value="s"
    @refresh="refresh"
    @toggle-pause="togglePause"
  >
    <ul class="divide-y">
      <li v-for="l in links" :key="l.to">
        <RouterLink :to="l.to" class="flex items-center justify-between py-2 hover:underline">
          <span>{{ l.label }}</span>
          <span class="font-mono text-sm tabular-nums text-muted-foreground">{{ l.summary }}</span>
        </RouterLink>
      </li>
    </ul>
  </HardwareTestPage>
</template>
