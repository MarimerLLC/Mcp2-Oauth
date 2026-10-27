<template>
  <div class="gtg dark end">
    <img class="end-logo" src="/img/techorama-orange.png" alt="Techorama">
    <img class="qr" src="/img/spreaview-qr.png" alt="SpreaView: review my session">
    <div class="end-text">
      <slot />
    </div>
    <LogoStrip />
  </div>
</template>

<style scoped>
.end {
  background: #222 url('/img/bg-stone.jpg') center / cover no-repeat;
}

.end-logo {
  position: absolute;
  top: 178px;
  left: 50%;
  transform: translateX(-50%);
  width: 480px;
}

.qr {
  position: absolute;
  top: 6px;
  right: 6px;
  width: 380px;
}

.end-text {
  position: absolute;
  top: 452px;
  left: 0;
  right: 0;
  text-align: center;
  color: #fff;
}

.end-text :deep(h1) {
  font-family: var(--gtg-body-font);
  font-weight: 400;
  font-size: 40px;
  letter-spacing: 0.18em;
  margin: 0;
}

.end-text :deep(p) {
  font-size: 21px;
  margin: 26px 0 0;
}
</style>
