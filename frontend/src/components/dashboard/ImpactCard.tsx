import { HorizontalBarChart } from '@/components/charts/HorizontalBarChart'
import { ChartCard } from '@/components/charts/ChartCard'
import { Skeleton } from '@/components/ui/skeleton'
import { formatCurrency, formatNumber } from '@/lib/format'
import { IMPACT_CATEGORY_LABELS } from '@/lib/labels'
import type { ImpactSummary } from '@/types/analysis'

/** Impacto financiero estimado: total destacado y desglose por categoría (barras horizontales). */
export function ImpactCard({
  impact,
  loading,
}: {
  impact: ImpactSummary | undefined
  loading?: boolean
}) {
  if (loading || !impact) return <Skeleton className="h-80" />
  const currency = impact.currency
  const rows = impact.byCategory
    .filter((c) => c.estimatedLoss > 0)
    .map((c) => ({
      label: IMPACT_CATEGORY_LABELS[c.category],
      value: c.estimatedLoss,
      problems: c.problems,
    }))
  const money = (v: number) => formatCurrency(v, currency)
  const compact = (v: number) => formatCurrency(v, currency, { compact: true })

  return (
    <ChartCard
      className="h-full"
      title="Impacto financiero estimado"
      subtitle={`Pérdida anual estimada por los problemas de ${impact.year ?? '—'} (${currency})`}
      ariaLabel={`Impacto total ${money(impact.totalEstimatedLoss)}. ${rows.map((r) => `${r.label}: ${money(r.value)}`).join('. ')}`}
      table={{
        columns: [
          { label: 'Categoría' },
          { label: 'Problemas', numeric: true },
          { label: 'Pérdida estimada', numeric: true },
        ],
        rows: rows.map((r) => [r.label, formatNumber(r.problems), money(r.value)]),
      }}
    >
      <div className="space-y-4">
        <div>
          <p className="text-kpi tabular-nums text-foreground">
            {money(impact.totalEstimatedLoss)}
          </p>
          <p className="text-small text-foreground-secondary">
            {impact.lossPercentOfRevenue != null
              ? `${formatNumber(impact.lossPercentOfRevenue, 1)} % de los ingresos · `
              : ''}
            {formatNumber(impact.quantifiedProblems)} problemas con pérdida estimada
            {impact.unquantifiedProblems > 0
              ? ` · ${formatNumber(impact.unquantifiedProblems)} alertas sin cuantificar`
              : ''}
          </p>
        </div>
        {rows.length > 0 ? (
          <HorizontalBarChart
            data={rows}
            valueName="Pérdida estimada"
            formatValue={money}
            formatAxis={compact}
          />
        ) : (
          <p className="text-small text-foreground-secondary">
            No hay pérdidas estimadas para este año.
          </p>
        )}
      </div>
    </ChartCard>
  )
}
