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
const cpu = computed(() => store.sensors?.cpu ?? null)
</script>

<template>
  <HardwareTestPage
    title="CPU"
    endpoint="/api/hardware/sensors"
    :meta="store.sensorsMeta"
    :paused="paused"
    :loading="store.sensors === null && !store.error"
    :error="store.sensorsMeta.ok ? null : store.error"
    :raw-value="cpu"
    @refresh="refresh"
    @toggle-pause="togglePause"
  >
    <template v-if="cpu">
      <FieldRow label="logicalCores" :value="cpu.logicalCores" />
      <FieldRow label="processUsagePercent" :value="cpu.processUsagePercent" unit="%" />
    </template>
  </HardwareTestPage>
</template>
