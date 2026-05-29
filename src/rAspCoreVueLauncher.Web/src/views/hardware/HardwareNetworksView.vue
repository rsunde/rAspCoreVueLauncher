<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useHardwareStore } from '@/stores/hardware'
import HardwareTestPage from '@/components/diagnostics/HardwareTestPage.vue'
import FieldRow from '@/components/diagnostics/FieldRow.vue'

const store = useHardwareStore()
const paused = ref(false)
onMounted(() => store.startPolling())
onUnmounted(() => store.stopPolling())
function refresh() { store.loadSensors() }
function togglePause() {
  if (paused.value) { store.startPolling(); paused.value = false }
  else { store.stopPolling(); paused.value = true }
}
const networks = computed(() => store.sensors?.networks ?? [])
</script>

<template>
  <HardwareTestPage
    title="Networks"
    endpoint="/api/hardware/sensors"
    :meta="store.sensorsMeta"
    :paused="paused"
    :loading="store.sensors === null && !store.error"
    :error="store.error"
    :raw-value="networks"
    @refresh="refresh"
    @toggle-pause="togglePause"
  >
    <template v-if="store.sensors">
      <p v-if="networks.length === 0" class="text-sm text-muted-foreground">no interfaces</p>
      <div v-for="(n, i) in networks" :key="i" class="border-b border-border/40 py-2 last:border-0">
        <FieldRow label="name" :value="n.name" />
        <FieldRow label="description" :value="n.description" />
        <FieldRow label="status" :value="n.status" />
        <FieldRow label="isLoopback" :value="n.isLoopback" />
        <FieldRow label="ipAddresses" :value="n.ipAddresses.join(', ')" />
      </div>
    </template>
  </HardwareTestPage>
</template>
