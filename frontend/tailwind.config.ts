import type { Config } from 'tailwindcss'
import animate from 'tailwindcss-animate'

/**
 * Tailwind mapeado a los tokens de `src/styles/tokens.css` (copia de docs/diseno/tokens.css).
 * Los componentes solo usan estas clases; nunca colores, tamaños o sombras sueltos.
 * El modo oscuro lo resuelven los tokens: no hace falta usar la variante `dark:`.
 */
const token = (name: string) => `var(--${name})`

const semantic = (name: string) => ({
  DEFAULT: token(name),
  text: token(`${name}-text`),
  soft: token(`${name}-soft`),
})

export default {
  darkMode: [
    'variant',
    [
      '@media (prefers-color-scheme: dark) { &:not(:where([data-theme="light"], [data-theme="light"] *)) }',
      '&:where([data-theme="dark"], [data-theme="dark"] *)',
    ],
  ],
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    container: {
      center: true,
      padding: { DEFAULT: '1rem', lg: '2rem' },
      screens: { '2xl': '1440px' },
    },
    extend: {
      colors: {
        background: token('background'),
        foreground: {
          DEFAULT: token('foreground'),
          secondary: token('foreground-secondary'),
          muted: token('foreground-muted'),
        },
        surface: {
          DEFAULT: token('surface'),
          muted: token('surface-muted'),
        },
        card: { DEFAULT: token('card'), foreground: token('card-foreground') },
        popover: { DEFAULT: token('popover'), foreground: token('popover-foreground') },
        muted: { DEFAULT: token('muted'), foreground: token('muted-foreground') },
        border: token('border'),
        input: token('input'),
        ring: token('ring'),
        primary: {
          DEFAULT: token('primary'),
          hover: token('primary-hover'),
          foreground: token('primary-foreground'),
          soft: token('primary-soft'),
          text: token('primary-text'),
        },
        secondary: { DEFAULT: token('secondary'), foreground: token('secondary-foreground') },
        accent: { DEFAULT: token('accent'), foreground: token('accent-foreground') },
        destructive: {
          DEFAULT: token('destructive'),
          foreground: token('destructive-foreground'),
        },
        sidebar: {
          DEFAULT: token('sidebar'),
          foreground: token('sidebar-foreground'),
          muted: token('sidebar-muted'),
          active: token('sidebar-active'),
        },
        critical: semantic('critical'),
        serious: semantic('serious'),
        warning: semantic('warning'),
        good: semantic('good'),
        info: semantic('info'),
        chart: {
          1: token('chart-1'),
          2: token('chart-2'),
          3: token('chart-3'),
          4: token('chart-4'),
          5: token('chart-5'),
          6: token('chart-6'),
          7: token('chart-7'),
          8: token('chart-8'),
          surface: token('chart-surface'),
          grid: token('chart-grid'),
          axis: token('chart-axis'),
          'diverging-mid': token('chart-diverging-mid'),
        },
      },
      borderRadius: {
        sm: token('radius-sm'),
        DEFAULT: token('radius'),
        md: token('radius'),
        lg: token('radius-lg'),
      },
      boxShadow: {
        sm: token('shadow-sm'),
        DEFAULT: token('shadow-sm'),
        md: token('shadow-md'),
        lg: token('shadow-lg'),
      },
      fontFamily: {
        sans: token('font-sans'),
        mono: token('font-mono'),
      },
      // Escala tipográfica de la guía (sección 3).
      fontSize: {
        display: ['48px', { lineHeight: '56px', fontWeight: '700' }],
        h1: ['30px', { lineHeight: '36px', fontWeight: '600' }],
        h2: ['22px', { lineHeight: '28px', fontWeight: '600' }],
        h3: ['18px', { lineHeight: '26px', fontWeight: '600' }],
        kpi: ['28px', { lineHeight: '34px', fontWeight: '600' }],
        body: ['15px', { lineHeight: '24px' }],
        small: ['13px', { lineHeight: '20px' }],
        caption: ['12px', { lineHeight: '16px', fontWeight: '500' }],
        overline: ['11px', { lineHeight: '16px', letterSpacing: '0.06em', fontWeight: '600' }],
      },
      maxWidth: {
        content: '1440px',
      },
    },
  },
  plugins: [animate],
} satisfies Config
