import { CircleHelp } from 'lucide-react'

import { ScoreGauge } from '@/components/common/ScoreGauge'
import { TrendIndicator } from '@/components/common/TrendIndicator'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { SimpleTooltip } from '@/components/ui/tooltip'
import { formatNumber, formatPercent } from '@/lib/format'
import type { ScoreYear } from '@/types/analysis'

/** Score Opsynex: medidor, interpretación, variación anual y componentes con su peso. */
export function ScoreCard({ score, loading }: { score: ScoreYear | undefined; loading?: boolean }) {
  if (loading) return <Skeleton className="h-80" />
  return (
    <Card className="flex h-full flex-col gap-4 p-4 sm:p-5">
      <div className="space-y-0.5">
        <h2 className="text-h3 text-foreground">Score Opsynex</h2>
        <p className="text-small text-foreground-secondary">
          {score
            ? `Salud financiera y operacional de ${score.year}`
            : 'Sin datos suficientes para este año'}
        </p>
      </div>
      <div className="grid items-center gap-6 sm:grid-cols-[auto_1fr]">
        <div className="flex flex-col items-center gap-2">
          <ScoreGauge score={score?.totalScore} className="w-44" />
          {score ? (
            <p className="text-small text-foreground-secondary">{score.interpretationLabel}</p>
          ) : null}
          {score?.changeFromPrevious != null ? (
            <TrendIndicator
              value={score.changeFromPrevious}
              unit="points"
              higherIsBetter
              stableThreshold={0.5}
              suffix={` vs. ${score.year - 1}`}
            />
          ) : null}
        </div>
        {score ? (
          <ul className="space-y-3" aria-label="Componentes del score">
            {score.components.map((component) => (
              <li key={component.dimension} className="space-y-1">
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
                <div
                  className="h-2 overflow-hidden rounded-full bg-surface-muted"
                  aria-hidden="true"
                >
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
        ) : null}
      </div>
    </Card>
  )
}
