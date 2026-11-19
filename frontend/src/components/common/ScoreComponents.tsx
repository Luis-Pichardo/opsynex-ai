import { CircleHelp } from 'lucide-react'

import { SimpleTooltip } from '@/components/ui/tooltip'
import { formatNumber, formatPercent } from '@/lib/format'

export interface ScoreComponentItem {
  key: string
  name: string
  score: number | null
  /** Peso usado (después de redistribuir los que faltan). */
  weight: number
  missingReason: string | null
}

/** Componentes de un score: nombre, puntaje 0–100 con barra y peso usado. */
export function ScoreComponents({
  items,
  label = 'Componentes del score',
}: {
  items: ScoreComponentItem[]
  label?: string
}) {
  return (
    <ul className="space-y-3" aria-label={label}>
      {items.map((component) => (
        <li key={component.key} className="space-y-1">
          <div className="flex items-baseline justify-between gap-2 text-small">
            <span className="flex min-w-0 items-center gap-1 text-foreground">
              <span className="truncate">{component.name}</span>
              {component.missingReason ? (
                <SimpleTooltip
                  content={`${component.missingReason} Su peso se repartió entre los demás.`}
                >
                  <button
                    type="button"
                    className="text-foreground-muted"
                    aria-label={`Por qué falta ${component.name}`}
                  >
                    <CircleHelp className="size-3.5" aria-hidden="true" />
                  </button>
                </SimpleTooltip>
              ) : null}
            </span>
            <span className="font-medium tabular-nums text-foreground">
              {component.score === null ? 'Sin datos' : formatNumber(component.score)}
            </span>
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-surface-muted" aria-hidden="true">
            <div
              className="h-full rounded-full bg-chart-1"
              style={{ width: `${component.score ?? 0}%` }}
            />
          </div>
          <p className="text-caption text-foreground-muted">
            Peso {formatPercent(component.weight, 0)}
          </p>
        </li>
      ))}
    </ul>
  )
}
