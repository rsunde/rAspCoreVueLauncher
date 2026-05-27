import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '@/views/HomeView.vue'

const STORAGE_KEY = 'rAspCoreVueLauncher:token'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'home', component: HomeView },
    { path: '/about', name: 'about', component: () => import('@/views/AboutView.vue') },
    { path: '/login', name: 'login', component: () => import('@/views/LoginView.vue') },
  ],
})

router.beforeEach((to) => {
  if (to.name === 'login') return true
  const token = localStorage.getItem(STORAGE_KEY)
  if (!token) return { name: 'login' }
  return true
})
