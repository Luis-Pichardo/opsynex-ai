/**
 * Estilos de Recharts con los tokens (GUIA_DE_DISENO.md §7): grilla horizontal tenue, ejes sin
 * línea salvo la base, textos de ejes en `--foreground-muted` y `caption`. Los colores de serie
 * son `var(--chart-N)` en orden fijo por entidad.
 */
export const SERIES = {
  revenue: 'var(--chart-1)',
  cost: 'var(--chart-2)',
  ebitda: 'var(--chart-3)',
  opex: 'var(--chart-4)',
  overhead: 'var(--chart-5)',
  energy: 'var(--chart-6)',
  maintenance: 'var(--chart-7)',
  materialLoss: 'var(--chart-8)',
} as const

export const PRIMARY_SERIES = 'var(--chart-1)'

export const AXIS_TICK = { className: 'fill-foreground-muted text-caption' }

export const GRID_PROPS = {
  stroke: 'var(--chart-grid)',
  vertical: false,
  strokeDasharray: '0',
} as const

export const AXIS_LINE = { stroke: 'var(--chart-axis)' }

/** Cursor del tooltip en líneas: crosshair vertical. */
export const LINE_CURSOR = { stroke: 'var(--chart-axis)', strokeWidth: 1 }

/** Cursor del tooltip en barras: banda suave. */
export const BAR_CURSOR = { fill: 'var(--surface-muted)' }
