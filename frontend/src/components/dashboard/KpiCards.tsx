import { StatCard } from '@/components/common/StatCard'
import { formatKpi, kpiChange } from '@/lib/kpi-format'
import { DASHBOARD_KPIS, KPI_FORMULAS } from '@/lib/kpi-labels'
import type { KpiDefinition, KpiYear } from '@/types/analysis'

interface KpiCardsProps {
  definitions: KpiDefinition[]
  year: KpiYear | undefined
  currency: string
  loading?: boolean
}

/** Fila de KPIs principales con su variación anual. */
export function KpiCards({ definitions, year, currency, loading }: KpiCardsProps) {
  return (
    <div className="grid gap-3 sm:grid-cols-2 sm:gap-4 lg:grid-cols-5">
      {DASHBOARD_KPIS.map((code) => {
        const definition = definitions.find((d) => d.code === code)
        if (!definition && !loading) return null
        const trend = year?.trends?.kpis[code]
        const change = definition ? kpiChange(definition.unit, trend) : null
        return (
          <StatCard
            key={code}
            label={definition?.name ?? code}
            hint={KPI_FORMULAS[code]}
            loading={loading}
            value={definition ? formatKpi(year?.values[code], definition.unit, currency, true) : ''}
            trend={
              definition && change && year?.trends
                ? {
                    value: change.value,
                    unit: change.unit,
                    stableThreshold: change.stableThreshold,
                    higherIsBetter: definition.higherIsBetter,
                    suffix: ` vs. ${year.trends.previousYear}`,
                  }
                : undefined
            }
          />
        )
      })}
    </div>
  )
}
