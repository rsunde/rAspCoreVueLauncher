<script setup lang="ts">
import { onBeforeUnmount, onMounted } from 'vue'
import { useHardwareStore } from '@/stores/hardware'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import Clock from '@/components/Clock.vue'
import SensorsPanel from '@/components/SensorsPanel.vue'

const hardware = useHardwareStore()

onMounted(() => {
  hardware.loadInfo()
  hardware.startPolling(2000)
})
onBeforeUnmount(() => hardware.stopPolling())
</script>

<template>
  <section class="space-y-6">
    <header class="space-y-2">
      <h1 class="text-4xl font-semibold tracking-tight">rAspCoreVueLauncher</h1>
      <p class="text-muted-foreground">
        ASP.NET Core 10 + Vue 3 + Tauri + Capacitor template. Sensors below are polled live from
        <code class="rounded bg-muted px-1.5 py-0.5 text-sm">/api/hardware/sensors</code> every 2 seconds.
      </p>
    </header>

    <Clock :server-offset-ms="hardware.sensorClockOffsetMs" />

    <Card>
      <CardHeader>
        <CardTitle>Local machine</CardTitle>
        <CardDescription>Static info reported by the ASP.NET Core API process.</CardDescription>
      </CardHeader>
      <CardContent class="space-y-3">
        <p v-if="hardware.loading && !hardware.info" class="text-muted-foreground">Loading…</p>
        <p v-else-if="hardware.error && !hardware.info" class="text-destructive">{{ hardware.error }}</p>
        <dl v-else-if="hardware.info" class="grid grid-cols-1 gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
          <div><dt class="text-muted-foreground">Platform</dt><dd>{{ hardware.info.osPlatform }}</dd></div>
          <div><dt class="text-muted-foreground">OS</dt><dd>{{ hardware.info.osDescription }}</dd></div>
          <div><dt class="text-muted-foreground">Architecture</dt><dd>{{ hardware.info.osArchitecture }}</dd></div>
          <div><dt class="text-muted-foreground">Machine</dt><dd>{{ hardware.info.machineName }}</dd></div>
          <div><dt class="text-muted-foreground">Cores</dt><dd>{{ hardware.info.processorCount }}</dd></div>
          <div><dt class="text-muted-foreground">Memory</dt><dd>{{ hardware.info.totalMemoryMb }} MB</dd></div>
          <div class="sm:col-span-2"><dt class="text-muted-foreground">Runtime</dt><dd>{{ hardware.info.runtimeVersion }}</dd></div>
        </dl>
        <Button variant="outline" :disabled="hardware.loading" @click="hardware.loadInfo()">Refresh</Button>
      </CardContent>
    </Card>

    <section class="space-y-2">
      <h2 class="text-lg font-semibold tracking-tight">Sensors</h2>
      <SensorsPanel :sensors="hardware.sensors" />
    </section>
  </section>
</template>
