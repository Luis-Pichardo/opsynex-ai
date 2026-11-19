import { ArrowRight } from 'lucide-react'
import type { ReactNode } from 'react'

import { ScoreGauge } from '@/components/common/ScoreGauge'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { formatCurrency, formatNumber, formatYears } from '@/lib/format'
import type { AnalysisSummary } from '@/types/uploads'

interface AnalysisSummaryCardProps {
  summary: AnalysisSummary | undefined
  /** Resumen anterior (al volver a analizar): muestra lo que cambió. */
  previous?: AnalysisSummary
  /** Otros cambios a mostrar (p. ej. cuentas dudosas antes y después). */
  extraChanges?: { label: string; before: string; after: string }[]
  loading?: boolean
}

function Change({ label, before, after }: { label: string; before: string; after: string }) {
  if (before === after) return null
  return (
    <li className="flex items-center gap-2 text-small text-foreground">
      <span className="text-foreground-secondary">{label}</span>
      <span className="tabular-nums">{before}</span>
      <ArrowRight className="size-3.5 text-foreground-secondary" aria-label="pasó a" />
      <span className="font-medium tabular-nums">{after}</span>
    </li>
  )
}

function Metric({
  label,
  value,
  children,
}: {
  label: string
  value: string
  children?: ReactNode
}) {
  return (
    <div className="space-y-1">
      <p className="text-overline uppercase text-foreground-secondary">{label}</p>
      <p className="text-h3 tabular-nums text-foreground">{value}</p>
      {children}
    </div>
  )
}

/** En los cambios, con un decimal para que se note una variación pequeña. */
const preciseScore = (s: AnalysisSummary | undefined) =>
  s?.score == null ? '—' : formatNumber(s.score, 1)

/** Resumen del análisis automático: años, KPIs, problemas, score e impacto. */
export function AnalysisSummaryCard({
  summary,
  previous,
  extraChanges = [],
  loading,
}: AnalysisSummaryCardProps) {
  if (loading || !summary) {
    return (
      <Card className="grid gap-6 p-4 sm:p-5 md:grid-cols-[auto_1fr]">
        <Skeleton className="mx-auto size-40 rounded-full" />
        <div className="grid gap-4 sm:grid-cols-2">
          {Array.from({ length: 4 }, (_, i) => (
            <Skeleton key={i} className="h-14" />
          ))}
        </div>
      </Card>
    )
  }

  const { problems } = summary
  return (
    <Card className="space-y-5 p-4 sm:p-5">
      <div className="grid items-center gap-6 md:grid-cols-[auto_1fr]">
        <ScoreGauge score={summary.score} className="w-40" />
        <div className="grid gap-4 sm:grid-cols-2">
          <Metric label="Años analizados" value={formatYears(summary.years)} />
          <Metric
            label="KPIs calculados"
            value={`${formatNumber(summary.kpisCalculated)} de ${formatNumber(summary.kpisTotal)}`}
          />
          <Metric
            label={`Problemas detectados${summary.latestYear ? ` (${summary.latestYear})` : ''}`}
            value={formatNumber(problems.total)}
          >
            {problems.total > 0 ? (
              <div className="flex flex-wrap gap-1">
                {problems.high > 0 ? (
                  <Badge variant="critical">{formatNumber(problems.high)} altas</Badge>
                ) : null}
                {problems.medium > 0 ? (
                  <Badge variant="serious">{formatNumber(problems.medium)} medias</Badge>
                ) : null}
                {problems.low > 0 ? (
                  <Badge variant="warning">{formatNumber(problems.low)} bajas</Badge>
                ) : null}
              </div>
            ) : null}
          </Metric>
          <Metric
            label="Impacto estimado"
            value={formatCurrency(summary.estimatedLoss ?? 0, summary.currency, { compact: true })}
          />
        </div>
      </div>
      {previous ? (
        <div className="rounded bg-surface-muted p-3">
          <p className="text-small font-medium text-foreground">Qué cambió con el nuevo análisis</p>
          <ul className="mt-1 space-y-1">
            <Change label="Score" before={preciseScore(previous)} after={preciseScore(summary)} />
            <Change
              label="Problemas"
              before={formatNumber(previous.problems.total)}
              after={formatNumber(summary.problems.total)}
            />
            <Change
              label="KPIs calculados"
              before={formatNumber(previous.kpisCalculated)}
              after={formatNumber(summary.kpisCalculated)}
            />
            <Change
              label="Impacto estimado"
              before={formatCurrency(previous.estimatedLoss ?? 0, previous.currency, {
                compact: true,
              })}
              after={formatCurrency(summary.estimatedLoss ?? 0, summary.currency, {
                compact: true,
              })}
            />
            {extraChanges.map((change) => (
              <Change key={change.label} {...change} />
            ))}
          </ul>
          {preciseScore(previous) === preciseScore(summary) &&
          previous.problems.total === summary.problems.total ? (
            <p className="text-small text-foreground-secondary">
              El score y los problemas no cambiaron.
            </p>
          ) : null}
        </div>
      ) : null}
    </Card>
  )
}
