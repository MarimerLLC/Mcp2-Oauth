<template>
  <div class="gtg dark cover">
    <img class="cover-logo" src="/img/techorama-medieval.png" alt="Techorama 2026 Medieval Edition">
    <div class="cover-text">
      <slot />
    </div>
    <LogoStrip />
  </div>
</template>

<style scoped>
.cover {
  background: #1b1b1b url('/img/bg-dark-pattern.png') center / cover no-repeat;
}

.cover-logo {
  position: absolute;
  top: 70px;
  left: 50%;
  transform: translateX(-50%);
  width: 440px;
}

.cover-text {
  position: absolute;
  top: 478px;
  left: 0;
  right: 0;
  text-align: center;
  color: #fff;
}

.cover-text :deep(h1) {
  font-family: var(--gtg-title-font);
  font-weight: 700;
  font-size: 66px;
  line-height: 1.1;
  margin: 0;
}

.cover-text :deep(p) {
  font-size: 29px;
  margin: 12px 0 0;
}
</style>
