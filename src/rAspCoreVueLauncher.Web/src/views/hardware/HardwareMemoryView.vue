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
const memory = computed(() => store.sensors?.memory ?? null)
</script>

<template>
  <HardwareTestPage
    title="Memory"
    endpoint="/api/hardware/sensors"
    :meta="store.sensorsMeta"
    :paused="paused"
    :loading="store.sensors === null && !store.error"
    :error="store.error"
    :raw-value="memory"
    @refresh="refresh"
    @toggle-pause="togglePause"
  >
    <template v-if="memory">
      <FieldRow label="processWorkingSetMb" :value="memory.processWorkingSetMb" unit="MB" />
      <FieldRow label="totalAvailableMb" :value="memory.totalAvailableMb" unit="MB" />
    </template>
  </HardwareTestPage>
</template>
