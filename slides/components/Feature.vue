<script setup lang="ts">
withDefaults(defineProps<{
  title: string
  icon?: string
  num?: string | number
  color?: 'amber' | 'rust' | 'green'
  size?: number
}>(), {
  color: 'amber',
  size: 58,
})
</script>

<!-- An icon in a colored circle, a bold heading, and a muted description. -->
<template>
  <div class="feature">
    <IconBadge :icon="icon" :num="num" :color="color" :size="size" />
    <div class="feature-text">
      <h3>{{ title }}</h3>
      <div class="muted feature-desc">
        <slot />
      </div>
    </div>
  </div>
</template>

<style scoped>
.feature {
  display: flex;
  gap: 22px;
  align-items: flex-start;
}

.feature-text {
  padding-top: 2px;
}

.feature-desc {
  margin-top: 6px;
  font-size: 18.5px;
  line-height: 1.4;
}
</style>
