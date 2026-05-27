import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { api } from '@/api/client'

const STORAGE_KEY = 'rAspCoreVueLauncher:token'

interface User {
  id: string
  email: string
  roles: string[]
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem(STORAGE_KEY))
  const user = ref<User | null>(null)

  const isAuthenticated = computed(() => !!token.value)

  // Restore axios header if token already exists in storage
  if (token.value) {
    api.defaults.headers.common['Authorization'] = `Bearer ${token.value}`
  }

  async function login(email: string, password: string): Promise<void> {
    const response = await api.post<{ accessToken: string; expiresAt: string }>('/api/auth/login', {
      email,
      password,
    })
    token.value = response.data.accessToken
    localStorage.setItem(STORAGE_KEY, token.value)
    api.defaults.headers.common['Authorization'] = `Bearer ${token.value}`
  }

  function logout(): void {
    token.value = null
    user.value = null
    localStorage.removeItem(STORAGE_KEY)
    delete api.defaults.headers.common['Authorization']
  }

  async function fetchMe(): Promise<void> {
    const response = await api.get<User>('/api/auth/me')
    user.value = response.data
  }

  return { token, user, isAuthenticated, login, logout, fetchMe }
})
