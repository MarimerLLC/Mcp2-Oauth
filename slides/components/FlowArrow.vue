<script setup lang="ts">
withDefaults(defineProps<{
  dir?: 'right' | 'left' | 'down'
  color?: string
  length?: number
}>(), {
  dir: 'right',
  color: '#5a524a',
  length: 44,
})
</script>

<template>
  <svg
    v-if="dir !== 'down'"
    class="gtg-arrow"
    :width="length"
    height="12"
    :viewBox="`0 0 ${length} 12`"
    :style="{ transform: dir === 'left' ? 'scaleX(-1)' : undefined }"
  >
    <line x1="0" y1="6" :x2="length - 8" y2="6" :stroke="color" stroke-width="2" />
    <polygon :points="`${length - 9},1 ${length},6 ${length - 9},11`" :fill="color" />
  </svg>
  <svg v-else class="gtg-arrow" width="12" :height="length" :viewBox="`0 0 12 ${length}`">
    <line x1="6" y1="0" x2="6" :y2="length - 8" :stroke="color" stroke-width="2" />
    <polygon :points="`1,${length - 9} 6,${length} 11,${length - 9}`" :fill="color" />
  </svg>
</template>

<style scoped>
.gtg-arrow {
  flex-shrink: 0;
  display: block;
}
</style>
