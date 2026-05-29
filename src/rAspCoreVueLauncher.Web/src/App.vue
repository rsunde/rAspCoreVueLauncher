<script setup lang="ts">
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { Button } from '@/components/ui/button'

const auth = useAuthStore()
const router = useRouter()

function handleLogout() {
  auth.logout()
  router.push('/login')
}
</script>

<template>
  <div class="min-h-svh bg-background text-foreground">
    <header class="border-b">
      <nav class="mx-auto flex max-w-4xl items-center gap-6 px-6 py-4">
        <span class="text-sm font-semibold">rAspCoreVueLauncher</span>
        <RouterLink to="/" class="text-sm text-muted-foreground hover:text-foreground" active-class="text-foreground">Home</RouterLink>
        <RouterLink to="/about" class="text-sm text-muted-foreground hover:text-foreground" active-class="text-foreground">About</RouterLink>
        <RouterLink to="/hardware" class="text-sm text-muted-foreground hover:text-foreground" active-class="text-foreground">Hardware</RouterLink>
        <div class="ml-auto flex items-center gap-4">
          <Button
            v-if="auth.isAuthenticated"
            variant="ghost"
            size="sm"
            @click="handleLogout"
          >
            Log out
          </Button>
        </div>
      </nav>
    </header>
    <main class="mx-auto max-w-4xl px-6 py-10">
      <RouterView />
    </main>
  </div>
</template>
