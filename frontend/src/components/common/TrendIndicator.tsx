import { Minus, TrendingDown, TrendingUp } from 'lucide-react'

import { formatNumber, formatPercent, formatPp } from '@/lib/format'
import { trendKind } from '@/lib/trend'
import { cn } from '@/lib/utils'

export type TrendUnit = 'percent' | 'pp' | 'points'

interface TrendIndicatorProps {
  /** Variación: en % (−3,2 = −3,2 %) o en puntos porcentuales. */
  value: number | null | undefined
  unit?: TrendUnit
  /** `true` si subir es bueno para el indicador (margen); `false` si bajar es bueno (costos). */
  higherIsBetter: boolean
  /** Variación (en la misma unidad) que se considera estable. Por defecto ±1. */
  stableThreshold?: number
  /** Texto adicional, p. ej. "vs. 2023". */
  suffix?: string
  className?: string
}

/**
 * Tendencia (GUIA_DE_DISENO.md §2.5): la flecha indica la dirección real; el color, si es
 * favorable (verde) o desfavorable (rojo). Estable: gris con `minus`.
 */
export function TrendIndicator({
  value,
  unit = 'percent',
  higherIsBetter,
  stableThreshold = 1,
  suffix,
  className,
}: TrendIndicatorProps) {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return <span className={cn('text-small text-foreground-muted', className)}>Sin variación</span>
  }

  const kind = trendKind(value, higherIsBetter, stableThreshold)
  const Icon = kind === 'stable' ? Minus : value > 0 ? TrendingUp : TrendingDown
  const magnitude =
    unit === 'pp'
      ? formatPp(Math.abs(value)).replace('+', '')
      : unit === 'points'
        ? `${formatNumber(Math.abs(value), 1)} pts`
        : formatPercent(Math.abs(value) / 100)
  const direction = kind === 'stable' ? 'estable' : value > 0 ? 'sube' : 'baja'
  const meaning = kind === 'favorable' ? 'favorable' : kind === 'unfavorable' ? 'desfavorable' : ''

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 text-small font-medium tabular-nums',
        kind === 'favorable' && 'text-good-text',
        kind === 'unfavorable' && 'text-critical-text',
        kind === 'stable' && 'text-foreground-secondary',
        className,
      )}
    >
      <Icon className="size-4 shrink-0" aria-hidden="true" />
      <span className="sr-only">{`${direction}${meaning ? `, ${meaning}` : ''}: `}</span>
      {magnitude}
      {suffix ? <span className="font-normal text-foreground-secondary">{suffix}</span> : null}
    </span>
  )
}
