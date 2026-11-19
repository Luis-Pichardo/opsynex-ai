import { ArrowRight } from 'lucide-react'
import { Link } from 'react-router'

import { ScoreComponents } from '@/components/common/ScoreComponents'
import { ScoreGauge } from '@/components/common/ScoreGauge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { formatNumber } from '@/lib/format'
import type { IndustrialScore } from '@/types/industrial'

interface IndustrialScoreCardProps {
  score: IndustrialScore | undefined
  loading?: boolean
  /** Descripción del alcance ("Planta, todo 2024"). */
  scope?: string
  /** Muestra el enlace a la página de Producción (dashboard). */
  showLink?: boolean
}

/**
 * Score industrial (PLATAFORMA_DE_ANALISIS §8): producción 30 %, costo 25 %, financiero 25 % y
 * estándar 20 %, con el mismo medidor y bandas que el Score Opsynex.
 */
export function IndustrialScoreCard({ score, loading, scope, showLink }: IndustrialScoreCardProps) {
  if (loading) return <Skeleton className="h-80" />
  const hasScore = score?.totalScore !== null && score?.totalScore !== undefined
  return (
    <Card className="flex h-full flex-col gap-4 p-4 sm:p-5">
      <div className="flex items-start justify-between gap-3">
        <div className="space-y-0.5">
          <h2 className="text-h3 text-foreground">Score industrial</h2>
          <p className="text-small text-foreground-secondary">
            {hasScore
              ? (scope ?? `Desempeño de la planta en ${score.year}`)
              : (score?.message ?? 'Sin datos suficientes')}
          </p>
        </div>
        {showLink ? (
          <Button asChild variant="ghost" size="sm" className="shrink-0">
            <Link to="/production">
              Ver producción
              <ArrowRight />
            </Link>
          </Button>
        ) : null}
      </div>
      <div className="grid items-center gap-6 sm:grid-cols-[auto_1fr]">
        <div className="flex flex-col items-center gap-2">
          <ScoreGauge score={score?.totalScore} name="Score industrial" className="w-44" />
          {hasScore ? (
            <p className="text-small text-foreground-secondary">{score.interpretationLabel}</p>
          ) : null}
          {score?.opsynexScore != null ? (
            <p className="text-caption text-foreground-muted">
              Score Opsynex del año: {formatNumber(score.opsynexScore)}
            </p>
          ) : null}
        </div>
        {score && score.components.length > 0 ? (
          <ScoreComponents
            label="Componentes del score industrial"
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
