<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(defineProps<{
  /** File in public/img/icons; ".png" is assumed when no extension is given. */
  icon?: string
  /** Shown instead of an icon, for numbered steps. */
  num?: string | number
  color?: 'amber' | 'rust' | 'green'
  size?: number
}>(), {
  color: 'amber',
  size: 58,
})

const src = computed(() => {
  if (!props.icon)
    return undefined
  return `/img/icons/${props.icon.includes('.') ? props.icon : `${props.icon}.png`}`
})
</script>

<template>
  <div class="icon-badge" :class="color" :style="{ width: `${size}px`, height: `${size}px` }">
    <img v-if="src" :src="src" alt="" :style="{ width: `${size * 0.58}px`, height: `${size * 0.58}px` }">
    <span v-else-if="num !== undefined" :style="{ fontSize: `${size * 0.4}px` }">{{ num }}</span>
  </div>
</template>

<style scoped>
.icon-badge {
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  color: #fff;
  font-weight: 700;
}

.amber {
  background: var(--gtg-amber);
}

.rust {
  background: var(--gtg-rust);
}

.green {
  background: var(--gtg-green);
}
</style>
