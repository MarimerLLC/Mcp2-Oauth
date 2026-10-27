<script setup lang="ts">
import { useSlideContext } from '@slidev/client'

withDefaults(defineProps<{
  subtitle?: string
  /** Background image: the icon pattern from the template, or the fleur-de-lis tile. */
  bg?: 'pattern' | 'fleur'
}>(), {
  bg: 'pattern',
})

// Slidev keeps `title` for navigation and doesn't pass it to layouts as a prop.
const { $frontmatter } = useSlideContext()
</script>

<template>
  <div class="gtg dark" :class="`bg-${bg}`">
    <header class="gtg-header">
      <h1 class="gtg-title">
        {{ $frontmatter.title }}
      </h1>
      <p v-if="subtitle" class="gtg-subtitle">
        {{ subtitle }}
      </p>
    </header>
    <div class="gtg-body">
      <slot />
    </div>
  </div>
</template>

<style scoped>
.bg-pattern {
  background: #1b1b1b url('/img/bg-dark-pattern.png') center / cover no-repeat;
}

.bg-fleur {
  background: #161616 url('/img/bg-fleur.jpg') center / cover no-repeat;
}

.gtg-header {
  position: absolute;
  top: 58px;
  left: 74px;
  right: 74px;
}
</style>
