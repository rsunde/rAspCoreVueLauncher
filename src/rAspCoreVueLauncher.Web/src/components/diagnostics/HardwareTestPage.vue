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
}>(), { meta: null, live: true, paused: false, loading: false, error: null })

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
