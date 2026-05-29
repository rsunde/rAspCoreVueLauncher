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
const battery = computed(() => store.sensors?.battery ?? null)
</script>

<template>
  <HardwareTestPage
    title="Battery"
    endpoint="/api/hardware/sensors"
    :meta="store.sensorsMeta"
    :paused="paused"
    :loading="store.sensors === null && !store.error"
    :error="store.sensorsMeta.ok ? null : store.error"
    :raw-value="battery"
    @refresh="refresh"
    @toggle-pause="togglePause"
  >
    <template v-if="store.sensors">
      <p v-if="!battery" class="text-sm text-muted-foreground">no battery present</p>
      <template v-else>
        <FieldRow label="percentRemaining" :value="battery.percentRemaining" unit="%" />
        <FieldRow label="isCharging" :value="battery.isCharging" />
        <FieldRow label="estimatedRuntime" :value="battery.estimatedRuntime" />
      </template>
    </template>
  </HardwareTestPage>
</template>
