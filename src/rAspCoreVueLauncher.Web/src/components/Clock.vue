<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

const props = defineProps<{
  /** (serverNowMs - clientNowMs) at last sync; 0 if unknown. */
  serverOffsetMs?: number
}>()

const now = ref(Date.now())
let handle: number | null = null

onMounted(() => {
  handle = window.setInterval(() => { now.value = Date.now() }, 250)
})
onBeforeUnmount(() => {
  if (handle !== null) window.clearInterval(handle)
})

const localTime = computed(() => new Date(now.value))
const serverTime = computed(() => new Date(now.value + (props.serverOffsetMs ?? 0)))

const localFmt = computed(() =>
  localTime.value.toLocaleString(undefined, {
    weekday: 'short', year: 'numeric', month: 'short', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
  }),
)
const serverFmt = computed(() => serverTime.value.toISOString().replace('T', ' ').replace(/\..+/, ' UTC'))
const offsetFmt = computed(() => {
  const ms = props.serverOffsetMs ?? 0
  const sign = ms >= 0 ? '+' : '-'
  return `${sign}${Math.abs(ms)} ms`
})
</script>

<template>
  <div class="rounded-lg border bg-card p-4 text-card-foreground shadow-sm">
    <div class="flex items-baseline justify-between">
      <div>
        <div class="text-xs uppercase tracking-wide text-muted-foreground">Local</div>
        <div class="font-mono text-lg tabular-nums">{{ localFmt }}</div>
      </div>
      <div class="text-right">
        <div class="text-xs uppercase tracking-wide text-muted-foreground">Server</div>
        <div class="font-mono text-lg tabular-nums">{{ serverFmt }}</div>
      </div>
    </div>
    <div class="mt-2 text-xs text-muted-foreground">
      Drift: <span class="font-mono">{{ offsetFmt }}</span>
    </div>
  </div>
</template>
