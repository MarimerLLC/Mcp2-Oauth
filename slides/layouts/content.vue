<script setup lang="ts">
import { useSlideContext } from '@slidev/client'

defineProps<{
  subtitle?: string
  /** Draws the orange medieval border around the slide. */
  frame?: boolean
}>()

// Slidev keeps `title` for navigation and doesn't pass it to layouts as a prop.
const { $frontmatter } = useSlideContext()
</script>

<template>
  <div class="gtg light" :class="{ framed: frame }">
    <img class="watermark" src="/img/knight-watermark.png" alt="">
    <img v-if="frame" class="frame" src="/img/frame-orange.png" alt="">
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
    <img class="footer-logo" src="/img/techorama-footer.jpg" alt="Techorama">
  </div>
</template>

<style scoped>
.light {
  background: #fff;
}

.watermark {
  position: absolute;
  top: 26px;
  right: -12px;
  height: 610px;
  pointer-events: none;
}

.frame {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  z-index: 10;
  pointer-events: none;
}

.gtg-header {
  position: absolute;
  top: 58px;
  left: 76px;
  right: 76px;
}

.footer-logo {
  position: absolute;
  right: 60px;
  bottom: 24px;
  width: 116px;
}

.framed .footer-logo {
  right: 52px;
  bottom: 50px;
}
</style>
