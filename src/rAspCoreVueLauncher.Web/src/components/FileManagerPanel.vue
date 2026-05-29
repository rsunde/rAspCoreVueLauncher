<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useFilesystemStore } from '@/stores/filesystem'
import type { FileEntry } from '@/types/filesystem'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import ConfirmDialog from '@/components/ConfirmDialog.vue'

const fs = useFilesystemStore()

onMounted(() => fs.list())

const crumbs = computed(() => {
  const p = fs.currentPath
  if (!p) return [] as { label: string; path: string }[]
  const sep = p.includes('\\') ? '\\' : '/'
  const parts = p.split(sep).filter(Boolean)
  const acc: { label: string; path: string }[] = []
  let cur = sep === '/' ? '' : ''
  for (const part of parts) {
    cur = cur ? `${cur}${sep}${part}` : (sep === '/' ? `/${part}` : `${part}${sep}`)
    acc.push({ label: part, path: cur })
  }
  return acc
})

function open(entry: FileEntry) {
  if (entry.isDirectory) fs.list(entry.path)
}

function goUp() {
  if (fs.parent) fs.list(fs.parent)
}

function fmtSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

const deleteTarget = ref<FileEntry | null>(null)
const deletePermanent = ref(false)

function askDelete(entry: FileEntry) {
  deleteTarget.value = entry
  deletePermanent.value = false
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  await fs.remove({ path: deleteTarget.value.path, permanent: deletePermanent.value })
  deleteTarget.value = null
}
</script>

<template>
  <Card>
    <CardHeader>
      <CardTitle>Files</CardTitle>
    </CardHeader>
    <CardContent>
      <div class="mb-3 flex flex-wrap items-center gap-1 text-sm">
        <Button variant="ghost" size="sm" @click="goUp" :disabled="!fs.parent">↑ Up</Button>
        <button class="hover:underline" @click="fs.list()">~</button>
        <template v-for="c in crumbs" :key="c.path">
          <span class="text-muted-foreground">/</span>
          <button class="hover:underline" @click="fs.list(c.path)">{{ c.label }}</button>
        </template>
      </div>

      <p v-if="fs.error" class="mb-2 text-sm text-red-600">{{ fs.error }}</p>
      <p v-if="fs.loading" class="text-sm text-muted-foreground">Loading…</p>

      <ul class="divide-y">
        <li
          v-for="entry in fs.entries"
          :key="entry.path"
          class="flex items-center justify-between py-2"
        >
          <button class="flex items-center gap-2 text-left hover:underline" @click="open(entry)">
            <span>{{ entry.isDirectory ? '📁' : '📄' }}</span>
            <span>{{ entry.name }}</span>
          </button>
          <div class="flex items-center gap-3">
            <span class="font-mono text-xs tabular-nums text-muted-foreground">
              {{ entry.isDirectory ? '' : fmtSize(entry.size) }}
            </span>
            <a
              v-if="!entry.isDirectory"
              :href="fs.downloadUrl(entry.path)"
              class="text-xs hover:underline"
            >Download</a>
            <Button variant="ghost" size="sm" @click="askDelete(entry)">Delete</Button>
          </div>
        </li>
      </ul>
    </CardContent>
  </Card>

  <ConfirmDialog
    :open="deleteTarget !== null"
    title="Delete entry"
    :message="`Delete '${deleteTarget?.name}'?`"
    confirm-label="Delete"
    danger
    @cancel="deleteTarget = null"
    @confirm="confirmDelete"
  >
    <label class="mt-4 flex items-center gap-2 text-sm">
      <input type="checkbox" v-model="deletePermanent" />
      Delete permanently (skip trash / recycle bin)
    </label>
  </ConfirmDialog>
</template>
