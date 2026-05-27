<script setup lang="ts">
import { computed } from 'vue'
import type { HardwareSensors, Vector3, Vector4 } from '@/types/hardware'

const props = defineProps<{ sensors: HardwareSensors | null }>()

function fmtMb(mb: number) {
  if (mb >= 1024) return `${(mb / 1024).toFixed(1)} GB`
  return `${mb} MB`
}

function fmtUptime(iso: string) {
  // C# TimeSpan serializes like "00:01:23.456" or "1.02:03:04.567"
  const m = /^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})/.exec(iso)
  if (!m) return iso
  const [, d, h, mm, s] = m
  const parts: string[] = []
  if (d) parts.push(`${d}d`)
  if (h !== '00' || d) parts.push(`${h}h`)
  parts.push(`${mm}m`)
  parts.push(`${s}s`)
  return parts.join(' ')
}

const memoryUsedPct = computed(() => {
  if (!props.sensors) return 0
  const { processWorkingSetMb, totalAvailableMb } = props.sensors.memory
  return totalAvailableMb > 0 ? Math.min(100, (processWorkingSetMb / totalAvailableMb) * 100) : 0
})

function fmtVec(v: Vector3 | Vector4 | null | undefined) {
  if (!v) return ''
  const parts = [v.x.toFixed(2), v.y.toFixed(2), v.z.toFixed(2)]
  if ('w' in v) parts.push((v as Vector4).w.toFixed(2))
  return parts.join(', ')
}

function fmtRelative(iso: string) {
  const t = new Date(iso).getTime()
  if (Number.isNaN(t)) return iso
  const diff = Math.max(0, Math.round((Date.now() - t) / 1000))
  if (diff < 60) return `${diff}s ago`
  if (diff < 3600) return `${Math.round(diff / 60)}m ago`
  if (diff < 86400) return `${Math.round(diff / 3600)}h ago`
  return `${Math.round(diff / 86400)}d ago`
}

function fmtVal(v: unknown): string {
  if (v === null || v === undefined) return ''
  if (typeof v === 'number') return Number.isInteger(v) ? v.toString() : v.toFixed(2)
  if (typeof v === 'boolean') return v ? 'yes' : 'no'
  if (typeof v === 'object' && 'x' in (v as object) && 'y' in (v as object) && 'z' in (v as object)) {
    return fmtVec(v as Vector3 | Vector4)
  }
  return String(v)
}

function humanLabel(key: string) {
  return key
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, c => c.toUpperCase())
    .trim()
}

function entries(obj: Record<string, unknown> | null | undefined) {
  if (!obj) return [] as Array<[string, unknown]>
  return Object.entries(obj).filter(([, v]) => v !== null && v !== undefined)
}

const mobileBlocks = computed(() => {
  const m = props.sensors?.mobile
  if (!m) return []
  return [
    { title: 'Device', data: m.device },
    { title: 'Motion', data: m.motion },
    { title: 'Orientation', data: m.orientation },
    { title: 'Environment', data: m.environment },
    { title: 'Location', data: m.location },
    { title: 'Health', data: m.health },
    { title: 'Biometric', data: m.biometric },
    { title: 'Connectivity', data: m.connectivity },
    { title: 'UserInterface', data: m.userInterface },
  ].filter(b => b.data && entries(b.data as Record<string, unknown>).length > 0)
})
</script>

<template>
  <div v-if="sensors" class="grid gap-4">
    <div class="grid gap-3 sm:grid-cols-2">
      <!-- CPU -->
      <div class="rounded-lg border bg-card p-4 text-card-foreground shadow-sm">
        <div class="mb-1 flex items-baseline justify-between">
          <span class="text-sm font-medium">API process CPU</span>
          <span class="font-mono text-lg tabular-nums">{{ sensors.cpu.processUsagePercent.toFixed(1) }}%</span>
        </div>
        <div class="h-2 w-full overflow-hidden rounded bg-muted">
          <div class="h-full bg-primary transition-[width] duration-500"
            :style="{ width: `${Math.min(100, sensors.cpu.processUsagePercent)}%` }" />
        </div>
        <p class="mt-2 text-xs text-muted-foreground">{{ sensors.cpu.logicalCores }} logical cores</p>
      </div>

      <!-- Memory -->
      <div class="rounded-lg border bg-card p-4 text-card-foreground shadow-sm">
        <div class="mb-1 flex items-baseline justify-between">
          <span class="text-sm font-medium">API process memory</span>
          <span class="font-mono text-lg tabular-nums">{{ fmtMb(sensors.memory.processWorkingSetMb) }}</span>
        </div>
        <div class="h-2 w-full overflow-hidden rounded bg-muted">
          <div class="h-full bg-primary transition-[width] duration-500" :style="{ width: `${memoryUsedPct}%` }" />
        </div>
        <p class="mt-2 text-xs text-muted-foreground">of {{ fmtMb(sensors.memory.totalAvailableMb) }} total</p>
      </div>
    </div>

    <!-- Uptime + battery -->
    <div class="rounded-lg border bg-card p-4 text-card-foreground shadow-sm">
      <div class="flex flex-wrap items-baseline justify-between gap-4">
        <div>
          <div class="text-xs uppercase tracking-wide text-muted-foreground">API process uptime</div>
          <div class="font-mono text-lg tabular-nums">{{ fmtUptime(sensors.processUptime) }}</div>
        </div>
        <div v-if="sensors.battery">
          <div class="text-xs uppercase tracking-wide text-muted-foreground">Battery</div>
          <div class="font-mono text-lg tabular-nums">
            {{ sensors.battery.percentRemaining }}%
            <span v-if="sensors.battery.isCharging" class="text-sm text-muted-foreground">(charging)</span>
          </div>
        </div>
        <div v-else class="text-xs text-muted-foreground italic">Battery: not implemented for this platform yet</div>
      </div>
    </div>

    <!-- Disks -->
    <div class="rounded-lg border bg-card p-4 text-card-foreground shadow-sm">
      <h3 class="mb-3 text-sm font-medium">Disks</h3>
      <div class="space-y-3">
        <div v-for="d in sensors.disks" :key="d.name" class="text-sm">
          <div class="mb-1 flex items-baseline justify-between gap-2">
            <span class="font-mono">{{ d.name }} <span class="text-xs text-muted-foreground">({{ d.driveFormat }})</span></span>
            <span class="font-mono tabular-nums text-muted-foreground">
              {{ fmtMb(d.totalMb - d.freeMb) }} / {{ fmtMb(d.totalMb) }}
            </span>
          </div>
          <div class="h-1.5 w-full overflow-hidden rounded bg-muted">
            <div class="h-full bg-primary transition-[width] duration-500"
              :style="{ width: `${d.totalMb > 0 ? ((d.totalMb - d.freeMb) / d.totalMb) * 100 : 0}%` }" />
          </div>
        </div>
      </div>
    </div>

    <!-- Network -->
    <div class="rounded-lg border bg-card p-4 text-card-foreground shadow-sm">
      <h3 class="mb-3 text-sm font-medium">Network</h3>
      <ul class="space-y-2 text-sm">
        <li v-for="n in sensors.networks" :key="n.name" class="flex flex-col gap-0.5 border-b pb-2 last:border-b-0 last:pb-0">
          <div class="flex items-baseline justify-between gap-2">
            <span class="font-medium">{{ n.name }}</span>
            <span class="text-xs text-muted-foreground">{{ n.status }}<span v-if="n.isLoopback"> · loopback</span></span>
          </div>
          <div class="text-xs text-muted-foreground">{{ n.description }}</div>
          <div class="flex flex-wrap gap-2 font-mono text-xs">
            <span v-for="ip in n.ipAddresses" :key="ip" class="rounded bg-muted px-1.5 py-0.5">{{ ip }}</span>
          </div>
        </li>
      </ul>
    </div>

    <!-- Mobile sensors -->
    <div v-if="sensors.mobile" class="rounded-lg border bg-card p-4 text-card-foreground shadow-sm">
      <div class="mb-3 flex items-baseline justify-between gap-2">
        <h3 class="text-sm font-medium">Mobile sensors</h3>
        <span class="font-mono text-xs text-muted-foreground tabular-nums">
          {{ sensors.mobile.clientId }} · {{ fmtRelative(sensors.mobile.capturedAtUtc) }}
        </span>
      </div>
      <div class="grid gap-3 sm:grid-cols-2">
        <div v-for="b in mobileBlocks" :key="b.title" class="rounded-lg border bg-card p-3 text-card-foreground shadow-sm">
          <h4 class="mb-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">{{ b.title }}</h4>
          <dl class="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-xs">
            <template v-for="[k, v] in entries(b.data as Record<string, unknown>)" :key="k">
              <dt class="text-muted-foreground">{{ humanLabel(k) }}</dt>
              <dd class="font-mono tabular-nums text-right">{{ fmtVal(v) }}</dd>
            </template>
          </dl>
        </div>
      </div>
    </div>
  </div>
  <p v-else class="text-sm text-muted-foreground">Waiting for first sensor reading…</p>
</template>
