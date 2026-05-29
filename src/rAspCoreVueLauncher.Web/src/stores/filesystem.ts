import { defineStore } from 'pinia'
import { ref } from 'vue'
import { api } from '@/api/client'
import type {
  DirectoryListing,
  FileEntry,
  WriteFileRequest,
  MkdirRequest,
  MoveRequest,
  CopyRequest,
  DeleteRequest,
} from '@/types/filesystem'

export const useFilesystemStore = defineStore('filesystem', () => {
  const currentPath = ref<string>('')
  const parent = ref<string | null>(null)
  const entries = ref<FileEntry[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  function describeError(e: unknown): string {
    if (typeof e === 'object' && e !== null && 'response' in e) {
      const resp = (e as { response?: { data?: { error?: string } } }).response
      if (resp?.data?.error) return resp.data.error
    }
    return e instanceof Error ? e.message : 'Unknown error'
  }

  async function list(path?: string) {
    loading.value = true
    error.value = null
    try {
      const { data } = await api.get<DirectoryListing>('/api/filesystem/list', {
        params: path ? { path } : undefined,
      })
      currentPath.value = data.path
      parent.value = data.parent
      entries.value = data.entries
    } catch (e) {
      error.value = describeError(e)
    } finally {
      loading.value = false
    }
  }

  async function read(path: string): Promise<string> {
    const { data } = await api.get<string>('/api/filesystem/read', { params: { path } })
    return data
  }

  function downloadUrl(path: string): string {
    const base = api.defaults.baseURL ?? ''
    return `${base.replace(/\/$/, '')}/api/filesystem/download?path=${encodeURIComponent(path)}`
  }

  async function write(req: WriteFileRequest) {
    await api.post('/api/filesystem/write', req)
    await list(currentPath.value)
  }

  async function mkdir(req: MkdirRequest) {
    await api.post('/api/filesystem/mkdir', req)
    await list(currentPath.value)
  }

  async function move(req: MoveRequest) {
    await api.post('/api/filesystem/move', req)
    await list(currentPath.value)
  }

  async function copy(req: CopyRequest) {
    await api.post('/api/filesystem/copy', req)
    await list(currentPath.value)
  }

  async function remove(req: DeleteRequest) {
    await api.post('/api/filesystem/delete', req)
    await list(currentPath.value)
  }

  return {
    currentPath, parent, entries, loading, error,
    list, read, downloadUrl, write, mkdir, move, copy, remove,
  }
})
