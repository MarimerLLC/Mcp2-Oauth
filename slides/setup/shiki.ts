import { defineShikiSetup } from '@slidev/types'

// Code always sits on dark panels, so use VS Code's Dark+ in both color schemes.
export default defineShikiSetup(() => ({
  themes: {
    dark: 'dark-plus',
    light: 'dark-plus',
  },
}))
