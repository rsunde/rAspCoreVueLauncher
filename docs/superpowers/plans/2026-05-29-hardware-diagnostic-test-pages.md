# Hardware Diagnostic Test Pages Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `/hardware` diagnostic hub plus one verification-oriented page per data group (info, CPU, memory, disks, networks, battery, mobile) that exhaustively surfaces everything the hardware layer exposes, with live updates, fetch status, raw-JSON toggle, and explicit empty/error states.

**Architecture:** Vue 3 SPA pages consume the existing `useHardwareStore` (which loads `/api/hardware/info` and polls `/api/hardware/sensors`). The store is extended with additive per-fetch metadata. Shared diagnostic chrome (`HardwareTestPage`) and a `FieldRow` primitive DRY the pages; a `MobileReadingView` renders an entire mobile reading and is reused for both server-cached devices and a "this client" panel fed by a new `peekLatestLocalReading()` accessor on the sensor bridge.

**Tech Stack:** Vue 3 `<script setup lang="ts">`, Pinia, vue-router, Tailwind, existing `ui/button` + `ui/card` (shadcn-vue). No new backend endpoints. No vitest (per spec): the gate is `vue-tsc` type-check + a manual verification checklist.

**Source-of-truth design:** `docs/superpowers/specs/2026-05-29-hardware-diagnostic-test-pages-design.md`

**Conventions:** All work is under `src/rAspCoreVueLauncher.Web/`. Run type-checks from that directory: `npx vue-tsc -b` (no output = success). The `@/` alias = `src/`. Commit after each task. The router has an auth `beforeEach` guard (redirects to `/login` without a localStorage token) — the new routes inherit it like `/`; no change needed.

---

## File Structure

**Modify:**
- `src/stores/hardware.ts` — add `FetchMeta` (exported) + `infoMeta`/`sensorsMeta`, populate in `loadInfo`/`loadSensors`.
- `src/lib/sensorsBridge.ts` — add module `activeBridge` singleton + `BridgeStatus` + `peekLatestLocalReading()`.
- `src/router/index.ts` — add 8 lazy `/hardware*` routes.
- `src/App.vue` — add a "Hardware" nav link.

**Create (shared diagnostics components):**
- `src/components/diagnostics/FieldRow.vue` — `label → value [unit]`, `—` for null/empty.
- `src/components/diagnostics/HardwareTestPage.vue` — header chrome (status, age, refresh/pause, raw-JSON) + slot.
- `src/components/diagnostics/MobileReadingView.vue` — renders one full `MobileSensorReading` (9 categories).

**Create (views):**
- `src/views/hardware/HardwareHubView.vue`, `HardwareInfoView.vue`, `HardwareCpuView.vue`, `HardwareMemoryView.vue`, `HardwareDisksView.vue`, `HardwareNetworksView.vue`, `HardwareBatteryView.vue`, `HardwareMobileView.vue`.

`main.ts` needs **no** change — `peekLatestLocalReading()` reads a module singleton that `startSensorBridge()` (already called in `main.ts`) sets internally.

---

# Phase 1 — Foundations

### Task 1: Store fetch metadata

**Files:**
- Modify: `src/rAspCoreVueLauncher.Web/src/stores/hardware.ts`

- [ ] **Step 1: Replace the store file** with this version (adds `FetchMeta` + two metas + timing; everything else preserved):

```typescript
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { api } from '@/api/client'
import type { HardwareInfo, HardwareSensors, MobileSensorReading } from '@/types/hardware'

export interface FetchMeta {
  fetchedAt: number | null   // Date.now() when the fetch completed
  durationMs: number | null
  httpStatus: number | null
  ok: boolean
}

function emptyMeta(): FetchMeta {
  return { fetchedAt: null, durationMs: null, httpStatus: null, ok: false }
}

export const useHardwareStore = defineStore('hardware', () => {
  const info = ref<HardwareInfo | null>(null)
  const sensors = ref<HardwareSensors | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const sensorClockOffsetMs = ref(0) // (server - client) at last sensor fetch
  const infoMeta = ref<FetchMeta>(emptyMeta())
  const sensorsMeta = ref<FetchMeta>(emptyMeta())

  let pollHandle: number | null = null

  function statusOf(e: unknown): number | null {
    return (e as { response?: { status?: number } }).response?.status ?? null
  }

  async function loadInfo() {
    loading.value = true
    error.value = null
    const started = performance.now()
    try {
      const res = await api.get<HardwareInfo>('/api/hardware/info')
      info.value = res.data
      infoMeta.value = { fetchedAt: Date.now(), durationMs: Math.round(performance.now() - started), httpStatus: res.status, ok: true }
    } catch (e) {
      infoMeta.value = { fetchedAt: Date.now(), durationMs: Math.round(performance.now() - started), httpStatus: statusOf(e), ok: false }
      error.value = e instanceof Error ? e.message : 'Unknown error'
    } finally {
      loading.value = false
    }
  }

  async function loadSensors() {
    const started = performance.now()
    try {
      const res = await api.get<HardwareSensors>('/api/hardware/sensors')
      sensors.value = res.data
      sensorClockOffsetMs.value = new Date(res.data.serverTimeUtc).getTime() - Date.now()
      sensorsMeta.value = { fetchedAt: Date.now(), durationMs: Math.round(performance.now() - started), httpStatus: res.status, ok: true }
      error.value = null
    } catch (e) {
      sensorsMeta.value = { fetchedAt: Date.now(), durationMs: Math.round(performance.now() - started), httpStatus: statusOf(e), ok: false }
      error.value = e instanceof Error ? e.message : 'Unknown error'
    }
  }

  async function postMobileSensors(reading: MobileSensorReading) {
    await api.post('/api/hardware/sensors/mobile', reading)
  }

  function startPolling(intervalMs = 2000) {
    if (pollHandle !== null) return
    loadSensors()
    pollHandle = window.setInterval(loadSensors, intervalMs)
  }

  function stopPolling() {
    if (pollHandle !== null) {
      window.clearInterval(pollHandle)
      pollHandle = null
    }
  }

  return { info, sensors, loading, error, sensorClockOffsetMs, infoMeta, sensorsMeta, loadInfo, loadSensors, postMobileSensors, startPolling, stopPolling }
})
```

- [ ] **Step 2: Type-check**

Run (from `src/rAspCoreVueLauncher.Web`): `npx vue-tsc -b`
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/stores/hardware.ts
git commit -m "feat(web): add fetch metadata to hardware store"
```

---

### Task 2: Sensor-bridge local-reading accessor

**Files:**
- Modify: `src/rAspCoreVueLauncher.Web/src/lib/sensorsBridge.ts`

- [ ] **Step 1: Add the module singleton + accessor.** Immediately after the `const CLIENT_ID_KEY = '...'` line (before `getOrCreateClientId`), add:

```typescript
export type BridgeStatus = 'not-started' | 'running'

// Set by startSensorBridge so components can read the latest local reading
// without holding the handle. peekLatestLocalReading() is used by the
// /hardware/mobile diagnostic page's "this client" panel.
let activeBridge: SensorBridgeHandle | null = null

export function peekLatestLocalReading(): { reading: MobileSensorReading | null; status: BridgeStatus } {
  if (!activeBridge) return { reading: null, status: 'not-started' }
  return { reading: activeBridge.latest(), status: 'running' }
}
```

- [ ] **Step 2: Register/clear the handle in `startSensorBridge`.** Find the end of `startSensorBridge`, which currently reads:

```typescript
  return {
    flush: post,
    stop: () => {
      if (timer != null) window.clearInterval(timer)
      if (geoWatchId != null && navigator.geolocation) navigator.geolocation.clearWatch(geoWatchId)
      window.removeEventListener('devicemotion', onMotion)
      window.removeEventListener('deviceorientation', onOrient)
    },
    latest: () => last,
  }
}
```

Replace it with (bind to a `handle` const, register it, clear on stop):

```typescript
  const handle: SensorBridgeHandle = {
    flush: post,
    stop: () => {
      if (timer != null) window.clearInterval(timer)
      if (geoWatchId != null && navigator.geolocation) navigator.geolocation.clearWatch(geoWatchId)
      window.removeEventListener('devicemotion', onMotion)
      window.removeEventListener('deviceorientation', onOrient)
      activeBridge = null
    },
    latest: () => last,
  }
  activeBridge = handle
  return handle
}
```

- [ ] **Step 2b: Verify the immediate-post line is intact** just above the return (it must remain):

```typescript
  timer = window.setInterval(post, intervalMs)
  // Fire one immediately so the server has a reading without waiting an interval.
  post()
```

- [ ] **Step 3: Type-check**

Run: `npx vue-tsc -b`
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/lib/sensorsBridge.ts
git commit -m "feat(web): expose peekLatestLocalReading() from sensor bridge"
```

---

### Task 3: `FieldRow.vue` primitive

**Files:**
- Create: `src/rAspCoreVueLauncher.Web/src/components/diagnostics/FieldRow.vue`

- [ ] **Step 1: Write the component**

```vue
<script setup lang="ts">
const props = defineProps<{ label: string; value: unknown; unit?: string }>()

const isEmpty = (v: unknown) => v === null || v === undefined || v === ''

function text(): string {
  if (isEmpty(props.value)) return '—'
  const base = typeof props.value === 'boolean' ? (props.value ? 'true' : 'false') : String(props.value)
  return props.unit ? `${base} ${props.unit}` : base
}
</script>

<template>
  <div class="flex items-baseline justify-between gap-4 border-b border-border/40 py-1 last:border-0">
    <span class="text-sm text-muted-foreground">{{ label }}</span>
    <span class="font-mono text-sm tabular-nums" :class="{ 'text-muted-foreground': isEmpty(value) }">{{ text() }}</span>
  </div>
</template>
```

- [ ] **Step 2: Type-check**

Run: `npx vue-tsc -b`
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/components/diagnostics/FieldRow.vue
git commit -m "feat(web): add FieldRow diagnostic primitive"
```

---

### Task 4: `HardwareTestPage.vue` shared chrome

**Files:**
- Create: `src/rAspCoreVueLauncher.Web/src/components/diagnostics/HardwareTestPage.vue`

- [ ] **Step 1: Write the component**

```vue
<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { Button } from '@/components/ui/button'
import type { FetchMeta } from '@/stores/hardware'

const props = withDefaults(defineProps<{
  title: string
  endpoint: string
  meta?: FetchMeta | null
  live?: boolean
  paused?: boolean
  loading?: boolean
  error?: string | null
  rawValue?: unknown
}>(), { meta: null, live: true, paused: false, loading: false, error: null, rawValue: null })

const emit = defineEmits<{ refresh: []; togglePause: [] }>()

const showRaw = ref(false)
const now = ref(Date.now())
let timer: number | undefined
onMounted(() => { timer = window.setInterval(() => { now.value = Date.now() }, 1000) })
onUnmounted(() => { if (timer != null) window.clearInterval(timer) })

const ageLabel = computed(() => {
  const at = props.meta?.fetchedAt
  if (!at) return 'never'
  const ms = Math.max(0, now.value - at)
  return ms < 1000 ? `${ms} ms ago` : `${(ms / 1000).toFixed(1)} s ago`
})

const indicator = computed(() => {
  if (props.error || props.meta?.ok === false) return { dot: '●', label: 'error', cls: 'text-red-600' }
  if (props.live && props.paused) return { dot: '⏸', label: 'paused', cls: 'text-muted-foreground' }
  if (props.live) return { dot: '●', label: 'live', cls: 'text-green-600' }
  return { dot: '●', label: 'static', cls: 'text-muted-foreground' }
})

const rawJson = computed(() => {
  try { return JSON.stringify(props.rawValue ?? null, null, 2) } catch { return '(unserializable)' }
})
</script>

<template>
  <section class="space-y-4">
    <header class="space-y-1">
      <div class="flex items-center gap-3">
        <h1 class="text-2xl font-semibold tracking-tight">{{ title }}</h1>
        <span :class="indicator.cls" class="text-sm">{{ indicator.dot }} {{ indicator.label }}</span>
      </div>
      <div class="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground">
        <code class="rounded bg-muted px-1.5 py-0.5">{{ endpoint }}</code>
        <span>fetched {{ ageLabel }}</span>
        <span v-if="meta?.httpStatus != null">status {{ meta.httpStatus }}</span>
        <span v-if="meta?.durationMs != null">{{ meta.durationMs }} ms</span>
      </div>
      <div class="flex flex-wrap items-center gap-2 pt-1">
        <Button variant="outline" size="sm" @click="emit('refresh')">Refresh now</Button>
        <Button v-if="live" variant="outline" size="sm" @click="emit('togglePause')">{{ paused ? 'Resume' : 'Pause' }}</Button>
        <Button variant="ghost" size="sm" @click="showRaw = !showRaw">{{ showRaw ? 'Hide' : 'Show' }} raw JSON</Button>
      </div>
    </header>

    <p v-if="error" class="rounded border border-red-600/40 bg-red-600/10 px-3 py-2 text-sm text-red-600">
      {{ error }}<span v-if="meta?.httpStatus != null"> (HTTP {{ meta.httpStatus }})</span>
    </p>
    <p v-else-if="loading" class="text-sm text-muted-foreground">Loading…</p>

    <div class="rounded-lg border bg-card p-4 text-card-foreground shadow-sm">
      <slot />
    </div>

    <pre v-if="showRaw" class="overflow-auto rounded-lg border bg-muted/50 p-4 text-xs">{{ rawJson }}</pre>
  </section>
</template>
```

- [ ] **Step 2: Type-check**

Run: `npx vue-tsc -b`
Expected: no errors. (`size="sm"` and `variant` `outline`/`ghost` exist in this project's `ui/button`.)

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/components/diagnostics/HardwareTestPage.vue
git commit -m "feat(web): add HardwareTestPage diagnostic chrome"
```

---

# Phase 2 — Server hardware pages

### Task 5: Machine info page (static)

**Files:**
- Create: `src/rAspCoreVueLauncher.Web/src/views/hardware/HardwareInfoView.vue`

- [ ] **Step 1: Write the view**

```vue
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
```

- [ ] **Step 2: Type-check** — `npx vue-tsc -b` (no errors).

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/views/hardware/HardwareInfoView.vue
git commit -m "feat(web): add hardware info diagnostic page"
```

---

### Task 6: CPU and Memory pages

**Files:**
- Create: `src/rAspCoreVueLauncher.Web/src/views/hardware/HardwareCpuView.vue`
- Create: `src/rAspCoreVueLauncher.Web/src/views/hardware/HardwareMemoryView.vue`

- [ ] **Step 1: Write `HardwareCpuView.vue`**

```vue
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
    :error="store.error"
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
```

- [ ] **Step 2: Write `HardwareMemoryView.vue`**

```vue
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
```

- [ ] **Step 3: Type-check** — `npx vue-tsc -b` (no errors).

- [ ] **Step 4: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/views/hardware/HardwareCpuView.vue src/rAspCoreVueLauncher.Web/src/views/hardware/HardwareMemoryView.vue
git commit -m "feat(web): add CPU and memory diagnostic pages"
```

---

### Task 7: Disks and Networks pages

**Files:**
- Create: `src/rAspCoreVueLauncher.Web/src/views/hardware/HardwareDisksView.vue`
- Create: `src/rAspCoreVueLauncher.Web/src/views/hardware/HardwareNetworksView.vue`

- [ ] **Step 1: Write `HardwareDisksView.vue`**

```vue
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
    :error="store.error"
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
```

- [ ] **Step 2: Write `HardwareNetworksView.vue`**

```vue
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
```

- [ ] **Step 3: Type-check** — `npx vue-tsc -b` (no errors).

- [ ] **Step 4: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/views/hardware/HardwareDisksView.vue src/rAspCoreVueLauncher.Web/src/views/hardware/HardwareNetworksView.vue
git commit -m "feat(web): add disks and networks diagnostic pages"
```

---

### Task 8: Battery page (nullable)

**Files:**
- Create: `src/rAspCoreVueLauncher.Web/src/views/hardware/HardwareBatteryView.vue`

- [ ] **Step 1: Write the view**

```vue
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
    :error="store.error"
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
```

- [ ] **Step 2: Type-check** — `npx vue-tsc -b` (no errors).

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/views/hardware/HardwareBatteryView.vue
git commit -m "feat(web): add battery diagnostic page"
```

---

# Phase 3 — Mobile sensors

### Task 9: `MobileReadingView.vue`

**Files:**
- Create: `src/rAspCoreVueLauncher.Web/src/components/diagnostics/MobileReadingView.vue`

- [ ] **Step 1: Write the component** (renders all 9 categories; vectors via `fmtVec`; absent category → "not reported")

```vue
<script setup lang="ts">
import type { MobileSensorReading, Vector3, Vector4 } from '@/types/hardware'
import FieldRow from '@/components/diagnostics/FieldRow.vue'

defineProps<{ reading: MobileSensorReading }>()

function fmtVec(v: Vector3 | Vector4 | null | undefined): string {
  if (!v) return '—'
  const parts = [`x ${v.x}`, `y ${v.y}`, `z ${v.z}`]
  if ('w' in v && (v as Vector4).w !== undefined) parts.push(`w ${(v as Vector4).w}`)
  return parts.join('   ')
}
</script>

<template>
  <div class="space-y-4">
    <div>
      <FieldRow label="clientId" :value="reading.clientId" />
      <FieldRow label="capturedAtUtc" :value="reading.capturedAtUtc" />
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">device</h3>
      <p v-if="!reading.device" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="manufacturer" :value="reading.device.manufacturer" />
        <FieldRow label="model" :value="reading.device.model" />
        <FieldRow label="osName" :value="reading.device.osName" />
        <FieldRow label="osVersion" :value="reading.device.osVersion" />
        <FieldRow label="locale" :value="reading.device.locale" />
        <FieldRow label="timeZone" :value="reading.device.timeZone" />
        <FieldRow label="isPhysicalDevice" :value="reading.device.isPhysicalDevice" />
      </template>
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">motion</h3>
      <p v-if="!reading.motion" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="accelerometer" :value="fmtVec(reading.motion.accelerometer)" />
        <FieldRow label="gyroscope" :value="fmtVec(reading.motion.gyroscope)" />
        <FieldRow label="magnetometer" :value="fmtVec(reading.motion.magnetometer)" />
        <FieldRow label="gravity" :value="fmtVec(reading.motion.gravity)" />
        <FieldRow label="linearAcceleration" :value="fmtVec(reading.motion.linearAcceleration)" />
        <FieldRow label="rotationVector" :value="fmtVec(reading.motion.rotationVector)" />
        <FieldRow label="userAcceleration" :value="fmtVec(reading.motion.userAcceleration)" />
        <FieldRow label="stepCount" :value="reading.motion.stepCount" />
        <FieldRow label="cadence" :value="reading.motion.cadence" />
      </template>
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">orientation</h3>
      <p v-if="!reading.orientation" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="pitch" :value="reading.orientation.pitch" />
        <FieldRow label="roll" :value="reading.orientation.roll" />
        <FieldRow label="yaw" :value="reading.orientation.yaw" />
        <FieldRow label="compassHeading" :value="reading.orientation.compassHeading" />
        <FieldRow label="trueHeading" :value="reading.orientation.trueHeading" />
        <FieldRow label="headingAccuracyDegrees" :value="reading.orientation.headingAccuracyDegrees" />
        <FieldRow label="screenOrientation" :value="reading.orientation.screenOrientation" />
      </template>
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">environment</h3>
      <p v-if="!reading.environment" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="ambientLightLux" :value="reading.environment.ambientLightLux" />
        <FieldRow label="proximityCm" :value="reading.environment.proximityCm" />
        <FieldRow label="isNear" :value="reading.environment.isNear" />
        <FieldRow label="ambientTemperatureCelsius" :value="reading.environment.ambientTemperatureCelsius" unit="°C" />
        <FieldRow label="relativeHumidityPercent" :value="reading.environment.relativeHumidityPercent" unit="%" />
        <FieldRow label="pressureHpa" :value="reading.environment.pressureHpa" unit="hPa" />
        <FieldRow label="altitudeMeters" :value="reading.environment.altitudeMeters" unit="m" />
        <FieldRow label="uvIndex" :value="reading.environment.uvIndex" />
      </template>
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">location</h3>
      <p v-if="!reading.location" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="latitude" :value="reading.location.latitude" />
        <FieldRow label="longitude" :value="reading.location.longitude" />
        <FieldRow label="altitudeMeters" :value="reading.location.altitudeMeters" unit="m" />
        <FieldRow label="accuracyMeters" :value="reading.location.accuracyMeters" unit="m" />
        <FieldRow label="altitudeAccuracyMeters" :value="reading.location.altitudeAccuracyMeters" unit="m" />
        <FieldRow label="headingDegrees" :value="reading.location.headingDegrees" />
        <FieldRow label="speedMetersPerSecond" :value="reading.location.speedMetersPerSecond" unit="m/s" />
        <FieldRow label="provider" :value="reading.location.provider" />
        <FieldRow label="isMocked" :value="reading.location.isMocked" />
        <FieldRow label="satelliteCount" :value="reading.location.satelliteCount" />
        <FieldRow label="fixTimestampUtc" :value="reading.location.fixTimestampUtc" />
      </template>
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">health</h3>
      <p v-if="!reading.health" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="heartRateBpm" :value="reading.health.heartRateBpm" unit="bpm" />
        <FieldRow label="heartRateVariabilityMs" :value="reading.health.heartRateVariabilityMs" unit="ms" />
        <FieldRow label="bloodOxygenPercent" :value="reading.health.bloodOxygenPercent" unit="%" />
        <FieldRow label="respiratoryRateBpm" :value="reading.health.respiratoryRateBpm" unit="bpm" />
        <FieldRow label="bodyTemperatureCelsius" :value="reading.health.bodyTemperatureCelsius" unit="°C" />
        <FieldRow label="skinTemperatureCelsius" :value="reading.health.skinTemperatureCelsius" unit="°C" />
        <FieldRow label="stepsToday" :value="reading.health.stepsToday" />
        <FieldRow label="distanceMetersToday" :value="reading.health.distanceMetersToday" unit="m" />
        <FieldRow label="activeEnergyKcalToday" :value="reading.health.activeEnergyKcalToday" unit="kcal" />
        <FieldRow label="vO2MaxMlPerKgPerMin" :value="reading.health.vO2MaxMlPerKgPerMin" />
        <FieldRow label="sleepStage" :value="reading.health.sleepStage" />
        <FieldRow label="stressLevel" :value="reading.health.stressLevel" />
      </template>
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">biometric</h3>
      <p v-if="!reading.biometric" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="fingerprintAvailable" :value="reading.biometric.fingerprintAvailable" />
        <FieldRow label="faceUnlockAvailable" :value="reading.biometric.faceUnlockAvailable" />
        <FieldRow label="irisAvailable" :value="reading.biometric.irisAvailable" />
        <FieldRow label="voiceUnlockAvailable" :value="reading.biometric.voiceUnlockAvailable" />
        <FieldRow label="strongBiometricEnrolled" :value="reading.biometric.strongBiometricEnrolled" />
        <FieldRow label="authenticationStatus" :value="reading.biometric.authenticationStatus" />
      </template>
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">connectivity</h3>
      <p v-if="!reading.connectivity" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="networkType" :value="reading.connectivity.networkType" />
        <FieldRow label="carrierName" :value="reading.connectivity.carrierName" />
        <FieldRow label="signalStrengthDbm" :value="reading.connectivity.signalStrengthDbm" unit="dBm" />
        <FieldRow label="wifiRssiDbm" :value="reading.connectivity.wifiRssiDbm" unit="dBm" />
        <FieldRow label="wifiSsid" :value="reading.connectivity.wifiSsid" />
        <FieldRow label="isMetered" :value="reading.connectivity.isMetered" />
        <FieldRow label="isRoaming" :value="reading.connectivity.isRoaming" />
        <FieldRow label="airplaneMode" :value="reading.connectivity.airplaneMode" />
        <FieldRow label="bluetoothEnabled" :value="reading.connectivity.bluetoothEnabled" />
        <FieldRow label="nfcAvailable" :value="reading.connectivity.nfcAvailable" />
        <FieldRow label="nfcEnabled" :value="reading.connectivity.nfcEnabled" />
      </template>
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">userInterface</h3>
      <p v-if="!reading.userInterface" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="screenBrightness" :value="reading.userInterface.screenBrightness" />
        <FieldRow label="keyguardLocked" :value="reading.userInterface.keyguardLocked" />
        <FieldRow label="appState" :value="reading.userInterface.appState" />
        <FieldRow label="hapticsAvailable" :value="reading.userInterface.hapticsAvailable" />
        <FieldRow label="flashlightOn" :value="reading.userInterface.flashlightOn" />
        <FieldRow label="ambientNoiseDb" :value="reading.userInterface.ambientNoiseDb" unit="dB" />
        <FieldRow label="headphonesPluggedIn" :value="reading.userInterface.headphonesPluggedIn" />
        <FieldRow label="isMuted" :value="reading.userInterface.isMuted" />
      </template>
    </div>
  </div>
</template>
```

- [ ] **Step 2: Type-check** — `npx vue-tsc -b` (no errors).

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/components/diagnostics/MobileReadingView.vue
git commit -m "feat(web): add MobileReadingView (all 9 sensor categories)"
```

---

### Task 10: Mobile page (cached devices + local bridge)

**Files:**
- Create: `src/rAspCoreVueLauncher.Web/src/views/hardware/HardwareMobileView.vue`

- [ ] **Step 1: Write the view**

```vue
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
    :error="store.error"
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
```

- [ ] **Step 2: Type-check** — `npx vue-tsc -b` (no errors).

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/views/hardware/HardwareMobileView.vue
git commit -m "feat(web): add mobile sensors diagnostic page (cached + local bridge)"
```

---

# Phase 4 — Hub & wiring

### Task 11: Hub page

**Files:**
- Create: `src/rAspCoreVueLauncher.Web/src/views/hardware/HardwareHubView.vue`

- [ ] **Step 1: Write the view**

```vue
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
    :error="store.error"
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
```

- [ ] **Step 2: Type-check** — `npx vue-tsc -b` (no errors).

- [ ] **Step 3: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/views/hardware/HardwareHubView.vue
git commit -m "feat(web): add hardware diagnostics hub page"
```

---

### Task 12: Wire routes + nav link

**Files:**
- Modify: `src/rAspCoreVueLauncher.Web/src/router/index.ts`
- Modify: `src/rAspCoreVueLauncher.Web/src/App.vue`

- [ ] **Step 1: Add the 8 routes.** In `src/router/index.ts`, inside the `routes: [...]` array, after the existing `{ path: '/login', ... }` line, add:

```typescript
    { path: '/hardware', name: 'hardware', component: () => import('@/views/hardware/HardwareHubView.vue') },
    { path: '/hardware/info', name: 'hardware-info', component: () => import('@/views/hardware/HardwareInfoView.vue') },
    { path: '/hardware/cpu', name: 'hardware-cpu', component: () => import('@/views/hardware/HardwareCpuView.vue') },
    { path: '/hardware/memory', name: 'hardware-memory', component: () => import('@/views/hardware/HardwareMemoryView.vue') },
    { path: '/hardware/disks', name: 'hardware-disks', component: () => import('@/views/hardware/HardwareDisksView.vue') },
    { path: '/hardware/networks', name: 'hardware-networks', component: () => import('@/views/hardware/HardwareNetworksView.vue') },
    { path: '/hardware/battery', name: 'hardware-battery', component: () => import('@/views/hardware/HardwareBatteryView.vue') },
    { path: '/hardware/mobile', name: 'hardware-mobile', component: () => import('@/views/hardware/HardwareMobileView.vue') },
```

(The existing `beforeEach` guard already allows any non-login route when a token is present — no guard change needed.)

- [ ] **Step 2: Add the nav link.** In `src/App.vue`, immediately after the existing About `<RouterLink>` line:

```html
        <RouterLink to="/about" class="text-sm text-muted-foreground hover:text-foreground" active-class="text-foreground">About</RouterLink>
```

add:

```html
        <RouterLink to="/hardware" class="text-sm text-muted-foreground hover:text-foreground" active-class="text-foreground">Hardware</RouterLink>
```

- [ ] **Step 3: Type-check** — `npx vue-tsc -b` (no errors; the lazy route imports now resolve to existing view files).

- [ ] **Step 4: Commit**

```bash
git add src/rAspCoreVueLauncher.Web/src/router/index.ts src/rAspCoreVueLauncher.Web/src/App.vue
git commit -m "feat(web): route hardware diagnostic pages + add Hardware nav link"
```

---

### Task 13: Final verification + design status bump

**Files:**
- Modify: `docs/superpowers/specs/2026-05-29-hardware-diagnostic-test-pages-design.md`

- [ ] **Step 1: Full production build**

Run (from `src/rAspCoreVueLauncher.Web`): `npm run build`
Expected: success (`vue-tsc -b && vite build`). Third-party Rolldown/`@vueuse` annotation notices from `node_modules` are pre-existing and acceptable; there must be **no** TypeScript errors from `src/`.

- [ ] **Step 2: Manual verification** (run the API: `dotnet run --project src/rAspCoreVueLauncher.Api`, and `npm run dev`; sign in if the auth guard requires it). Confirm:
  1. The "Hardware" nav link opens the hub; each hub row links to its page and back.
  2. Each live page shows values updating, a sensible "fetched … ago", and `status 200`.
  3. **Pause** stops updates (indicator → ⏸ paused); **Resume** restarts; **Refresh now** forces a fetch.
  4. **Show raw JSON** reveals JSON matching the rendered fields; **Hide** collapses it.
  5. Empty/null states render: battery "no battery present" on a machine without one; disks/networks populated; mobile "no devices reporting" until a client posts.
  6. `/hardware/info` is static (no Pause control) with a working Refresh.
  7. Mobile page: the "This client" panel shows status `running` and a local reading (device/locale/timeZone at minimum) within ~2 s; cached devices appear after a poll cycle.
  8. Stop the API → pages show the red error banner with an HTTP status, not a blank screen or crash.

  Report the result of each check.

- [ ] **Step 3: Bump the design status.** In `docs/superpowers/specs/2026-05-29-hardware-diagnostic-test-pages-design.md`, change:

```
**Status:** Approved design (pre-implementation)
```

to:

```
**Status:** Implemented (see docs/superpowers/plans/2026-05-29-hardware-diagnostic-test-pages.md)
```

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-05-29-hardware-diagnostic-test-pages-design.md
git commit -m "docs: mark hardware diagnostic test-pages design as implemented"
```

---

## Notes & decisions locked by this plan

- **Data sourcing (approach A):** all live pages read slices of the single `useHardwareStore` `/sensors` poll; only `/info` is separate. Pages call the existing `startPolling`/`stopPolling`/`loadSensors`/`loadInfo`.
- **Pause** is page-local state (`paused` ref) toggling `stop/startPolling` — `startPolling` is idempotent. Only one route mounts at a time, so polling lifecycles don't collide with `HomeView`.
- **Bridge status** is coarse and honest: `'not-started' | 'running'` (the bridge exposes no permission/unsupported state; it posts a reading immediately on start, so `latest()` is populated quickly even on desktop). The "this client" panel shows "no local reading captured yet" until `latest()` returns non-null.
- **Type bridge:** the sensor bridge's `MobileSensorReading` is structurally compatible with `@/types/hardware`'s; the local reading is cast for display.
- **No new backend endpoints, no vitest, no changes to `SensorsPanel.vue`/`HomeView`** beyond the additive store metadata. Auth guard untouched.
