import { ScoreComponents } from '@/components/common/ScoreComponents'
import { ScoreGauge } from '@/components/common/ScoreGauge'
import { TrendIndicator } from '@/components/common/TrendIndicator'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
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
          <ScoreComponents
            items={score.components.map((c) => ({
              key: c.dimension,
              name: c.name,
              score: c.score,
              weight: c.weight,
              missingReason: c.missingReason,
            }))}
          />
        ) : null}
      </div>
    </Card>
  )
}
