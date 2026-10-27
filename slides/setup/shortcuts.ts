import { defineShortcutsSetup } from '@slidev/types'

// PowerPoint habit: Down/Up step through builds like Right/Left, instead of Slidev's default
// of jumping a whole slide and skipping its remaining clicks. Shift+Right/Left still skip.
export default defineShortcutsSetup((nav, base) =>
  base.map((shortcut) => {
    if (shortcut.name === 'next_down')
      return { ...shortcut, fn: nav.next }
    if (shortcut.name === 'prev_up')
      return { ...shortcut, fn: nav.prev }
    return shortcut
  }),
)
