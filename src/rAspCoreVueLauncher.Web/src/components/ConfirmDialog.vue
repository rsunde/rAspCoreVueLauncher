<script setup lang="ts">
import { Button } from '@/components/ui/button'

defineProps<{
  open: boolean
  title: string
  message: string
  confirmLabel?: string
  danger?: boolean
}>()

const emit = defineEmits<{ confirm: []; cancel: [] }>()
</script>

<template>
  <div
    v-if="open"
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
    @click.self="emit('cancel')"
  >
    <div class="w-full max-w-md rounded-lg border bg-card p-6 text-card-foreground shadow-lg">
      <h2 class="text-lg font-semibold">{{ title }}</h2>
      <p class="mt-2 text-sm text-muted-foreground">{{ message }}</p>
      <slot />
      <div class="mt-6 flex justify-end gap-2">
        <Button variant="outline" @click="emit('cancel')">Cancel</Button>
        <Button :variant="danger ? 'destructive' : 'default'" @click="emit('confirm')">
          {{ confirmLabel ?? 'Confirm' }}
        </Button>
      </div>
    </div>
  </div>
</template>
