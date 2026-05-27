<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
</script>

<template>
  <div class="min-h-svh bg-background text-foreground">
    <header class="border-b">
      <nav class="mx-auto flex max-w-4xl items-center gap-6 px-6 py-4">
        <span class="text-sm font-semibold">rAspCoreVueLauncher</span>
        <RouterLink to="/" class="text-sm text-muted-foreground hover:text-foreground" active-class="text-foreground">Home</RouterLink>
        <RouterLink to="/about" class="text-sm text-muted-foreground hover:text-foreground" active-class="text-foreground">About</RouterLink>
      </nav>
    </header>
    <main class="mx-auto max-w-4xl px-6 py-10">
      <RouterView />
    </main>
  </div>
</template>
