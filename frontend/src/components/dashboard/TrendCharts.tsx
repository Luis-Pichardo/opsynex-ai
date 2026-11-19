import { useState } from 'react'

import { ChartCard } from '@/components/charts/ChartCard'
import { PRIMARY_SERIES, SERIES } from '@/components/charts/chart-theme'
import { GroupedBarChart } from '@/components/charts/GroupedBarChart'
import { TrendLineChart } from '@/components/charts/TrendLineChart'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { formatCurrency } from '@/lib/format'
import { formatKpi } from '@/lib/kpi-format'
import type { KpiCode, KpiDefinition, KpiYear } from '@/types/analysis'
import type { FinancialRecord } from '@/types/uploads'

interface TrendChartsProps {
  definitions: KpiDefinition[]
  years: KpiYear[]
  financial: FinancialRecord[]
  currency: string
}

const range = (years: number[]) =>
  years.length ? `${Math.min(...years)}–${Math.max(...years)}` : '—'

/** Tendencias: líneas multi-año con selector de KPI y barras de ingresos vs. costo total. */
export function TrendCharts({ definitions, years, financial, currency }: TrendChartsProps) {
  const available = definitions.filter((d) => years.some((y) => y.values[d.code] !== null))
  const [selected, setSelected] = useState<KpiCode>('EbitdaMargin')
  const definition = available.find((d) => d.code === selected) ?? available[0]
  const sortedYears = [...years].sort((a, b) => a.year - b.year)
  const kpiData = sortedYears.map((y) => ({
    year: String(y.year),
    value: definition ? y.values[definition.code] : null,
  }))
  const format = (v: number) =>
    definition ? formatKpi(v, definition.unit, currency, true) : String(v)

  const money = (v: number) => formatCurrency(v, currency)
  const compact = (v: number) => formatCurrency(v, currency, { compact: true })
  const financialData = [...financial]
    .sort((a, b) => a.year - b.year)
    .map((f) => ({
      year: String(f.year),
      revenue: f.revenue as number | null,
      totalCost: f.totalCost as number | null,
    }))
  const allYears = sortedYears.map((y) => y.year)

  return (
    <div className="grid gap-3 sm:gap-4 lg:grid-cols-2">
      <ChartCard
        title="Tendencia del KPI"
        subtitle={`${definition?.name ?? 'KPI'} por año, ${range(allYears)}`}
        ariaLabel={`${definition?.name}: ${kpiData.map((d) => `${d.year} ${d.value == null ? 'sin dato' : format(d.value)}`).join(', ')}`}
        actions={
          <Select value={definition?.code} onValueChange={(v) => setSelected(v as KpiCode)}>
            <SelectTrigger className="h-8 w-48 text-small" aria-label="KPI a graficar">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {available.map((d) => (
                <SelectItem key={d.code} value={d.code}>
                  {d.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        }
        table={{
          columns: [{ label: 'Año' }, { label: definition?.name ?? 'Valor', numeric: true }],
          rows: kpiData.map((d) => [d.year, d.value == null ? '—' : format(d.value)]),
        }}
      >
        <TrendLineChart
          data={kpiData}
          xKey="year"
          series={[{ key: 'value', name: definition?.name ?? 'Valor', color: PRIMARY_SERIES }]}
          formatValue={format}
        />
      </ChartCard>
      <ChartCard
        title="Ingresos vs. costo total"
        subtitle={`Montos anuales en ${currency}, ${range(financialData.map((d) => Number(d.year)))}`}
        ariaLabel={financialData
          .map(
            (d) =>
              `${d.year}: ingresos ${d.revenue == null ? 'sin dato' : money(d.revenue)}, costo total ${d.totalCost == null ? 'sin dato' : money(d.totalCost)}`,
          )
          .join('. ')}
        table={{
          columns: [
            { label: 'Año' },
            { label: 'Ingresos', numeric: true },
            { label: 'Costo total', numeric: true },
          ],
          rows: financialData.map((d) => [
            d.year,
            d.revenue == null ? '—' : money(d.revenue),
            d.totalCost == null ? '—' : money(d.totalCost),
          ]),
        }}
      >
        <GroupedBarChart
          data={financialData}
          xKey="year"
          series={[
            { key: 'revenue', name: 'Ingresos', color: SERIES.revenue },
            { key: 'totalCost', name: 'Costo total', color: SERIES.cost },
          ]}
          formatValue={money}
          formatAxis={compact}
        />
      </ChartCard>
    </div>
  )
}
