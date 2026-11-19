import { Badge } from '@/components/ui/badge'
import { formatNumber } from '@/lib/format'
import { scoreBand } from '@/lib/score'
import { cn } from '@/lib/utils'

const SIZE = 200
const STROKE = 12
const RADIUS = (SIZE - STROKE) / 2
const CIRCUMFERENCE = 2 * Math.PI * RADIUS
const ARC = (CIRCUMFERENCE * 240) / 360

const ARC_CLASS = {
  good: 'stroke-good',
  warning: 'stroke-warning',
  critical: 'stroke-critical',
} as const

interface ScoreGaugeProps {
  score: number | null | undefined
  className?: string
}

/**
 * ScoreGauge (GUIA_DE_DISENO.md §6): arco de 240° y 12 px; pista `--surface-muted`, progreso en
 * el color del rango. El número va en `--foreground` (`display`) y la etiqueta del rango debajo.
 */
export function ScoreGauge({ score, className }: ScoreGaugeProps) {
  const hasScore = score !== null && score !== undefined && !Number.isNaN(score)
  const value = hasScore ? Math.min(Math.max(score, 0), 100) : 0
  const info = hasScore ? scoreBand(value) : null
  const label = info
    ? `Score Opsynex ${formatNumber(value)} de 100: ${info.interpretation}`
    : 'Score Opsynex sin calcular'

  return (
    <div className={cn('relative mx-auto w-48', className)} role="img" aria-label={label}>
      <svg viewBox={`0 0 ${SIZE} ${SIZE}`} className="w-full" aria-hidden="true">
        <g transform={`rotate(150 ${SIZE / 2} ${SIZE / 2})`}>
          <circle
            cx={SIZE / 2}
            cy={SIZE / 2}
            r={RADIUS}
            fill="none"
            strokeWidth={STROKE}
            strokeLinecap="round"
            strokeDasharray={`${ARC} ${CIRCUMFERENCE}`}
            className="stroke-surface-muted"
          />
          {info && value > 0 ? (
            <circle
              cx={SIZE / 2}
              cy={SIZE / 2}
              r={RADIUS}
              fill="none"
              strokeWidth={STROKE}
              strokeLinecap="round"
              strokeDasharray={`${(ARC * value) / 100} ${CIRCUMFERENCE}`}
              className={cn(ARC_CLASS[info.band], 'transition-all duration-200 ease-out')}
            />
          ) : null}
        </g>
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center gap-1 pt-2">
        <span className="text-display tabular-nums text-foreground">
          {hasScore ? formatNumber(value) : '—'}
        </span>
        {info ? (
          <Badge variant={info.band}>{info.label}</Badge>
        ) : (
          <span className="text-small text-foreground-secondary">Sin datos</span>
        )}
      </div>
    </div>
  )
}
