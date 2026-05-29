import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '@/views/HomeView.vue'

const STORAGE_KEY = 'rAspCoreVueLauncher:token'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'home', component: HomeView },
    { path: '/about', name: 'about', component: () => import('@/views/AboutView.vue') },
    { path: '/login', name: 'login', component: () => import('@/views/LoginView.vue') },
    { path: '/hardware', name: 'hardware', component: () => import('@/views/hardware/HardwareHubView.vue') },
    { path: '/hardware/info', name: 'hardware-info', component: () => import('@/views/hardware/HardwareInfoView.vue') },
    { path: '/hardware/cpu', name: 'hardware-cpu', component: () => import('@/views/hardware/HardwareCpuView.vue') },
    { path: '/hardware/memory', name: 'hardware-memory', component: () => import('@/views/hardware/HardwareMemoryView.vue') },
    { path: '/hardware/disks', name: 'hardware-disks', component: () => import('@/views/hardware/HardwareDisksView.vue') },
    { path: '/hardware/networks', name: 'hardware-networks', component: () => import('@/views/hardware/HardwareNetworksView.vue') },
    { path: '/hardware/battery', name: 'hardware-battery', component: () => import('@/views/hardware/HardwareBatteryView.vue') },
    { path: '/hardware/mobile', name: 'hardware-mobile', component: () => import('@/views/hardware/HardwareMobileView.vue') },
  ],
})

router.beforeEach((to) => {
  if (to.name === 'login') return true
  const token = localStorage.getItem(STORAGE_KEY)
  if (!token) return { name: 'login' }
  return true
})
