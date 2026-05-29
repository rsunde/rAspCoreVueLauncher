<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useHardwareStore } from '@/stores/hardware'
import HardwareTestPage from '@/components/diagnostics/HardwareTestPage.vue'
import FieldRow from '@/components/diagnostics/FieldRow.vue'

const store = useHardwareStore()
onMounted(() => { if (!store.info) store.loadInfo() })
function refresh() { store.loadInfo() }
const info = computed(() => store.info)
</script>

<template>
  <HardwareTestPage
    title="Machine info"
    endpoint="/api/hardware/info"
    :meta="store.infoMeta"
    :live="false"
    :loading="store.info === null && !store.error"
    :error="store.error"
    :raw-value="info"
    @refresh="refresh"
  >
    <template v-if="info">
      <FieldRow label="osPlatform" :value="info.osPlatform" />
      <FieldRow label="osDescription" :value="info.osDescription" />
      <FieldRow label="osArchitecture" :value="info.osArchitecture" />
      <FieldRow label="machineName" :value="info.machineName" />
      <FieldRow label="processorCount" :value="info.processorCount" />
      <FieldRow label="totalMemoryMb" :value="info.totalMemoryMb" unit="MB" />
      <FieldRow label="runtimeVersion" :value="info.runtimeVersion" />
    </template>
  </HardwareTestPage>
</template>
