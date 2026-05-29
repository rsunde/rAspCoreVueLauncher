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
