<script setup lang="ts">
defineProps<{ label?: string }>()
</script>

<!-- Dark rounded panel for code, with an optional gold caption. -->
<template>
  <div class="panel">
    <div v-if="label" class="label">
      {{ label }}
    </div>
    <slot />
  </div>
</template>
