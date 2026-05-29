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
const disks = computed(() => store.sensors?.disks ?? [])
</script>

<template>
  <HardwareTestPage
    title="Disks"
    endpoint="/api/hardware/sensors"
    :meta="store.sensorsMeta"
    :paused="paused"
    :loading="store.sensors === null && !store.error"
    :error="store.sensorsMeta.ok ? null : store.error"
    :raw-value="disks"
    @refresh="refresh"
    @toggle-pause="togglePause"
  >
    <template v-if="store.sensors">
      <p v-if="disks.length === 0" class="text-sm text-muted-foreground">no drives reported</p>
      <div v-for="(d, i) in disks" :key="i" class="border-b border-border/40 py-2 last:border-0">
        <FieldRow label="name" :value="d.name" />
        <FieldRow label="driveFormat" :value="d.driveFormat" />
        <FieldRow label="totalMb" :value="d.totalMb" unit="MB" />
        <FieldRow label="freeMb" :value="d.freeMb" unit="MB" />
      </div>
    </template>
  </HardwareTestPage>
</template>
