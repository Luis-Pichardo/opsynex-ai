import { useQuery } from '@tanstack/react-query'
import { ChartColumn, CircleHelp } from 'lucide-react'

import { kpisApi } from '@/api/kpis'
import { ChartCard } from '@/components/charts/ChartCard'
import { PRIMARY_SERIES } from '@/components/charts/chart-theme'
import { TrendLineChart } from '@/components/charts/TrendLineChart'
import { EmptyState } from '@/components/common/EmptyState'
import { PageHeader } from '@/components/common/PageHeader'
import { RequireCompany } from '@/components/common/RequireCompany'
import { TrendIndicator } from '@/components/common/TrendIndicator'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { SimpleTooltip } from '@/components/ui/tooltip'
import { formatKpi, kpiChange } from '@/lib/kpi-format'
import { KPI_FORMULAS } from '@/lib/kpi-labels'
import { useWorkspace } from '@/workspace/useWorkspace'

/** KPIs: tabla histórica con la variación del último año y un gráfico por KPI. */
export function KpisPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="KPIs"
        description="Indicadores de rentabilidad, costos, productividad y estabilidad financiera por año."
      />
      <RequireCompany>
        <Kpis />
      </RequireCompany>
    </div>
  )
}

function Kpis() {
  const { companyId, currency } = useWorkspace()
  const kpis = useQuery({
    queryKey: ['kpis', 'all', companyId],
    queryFn: ({ signal }) => kpisApi.get(null, signal),
  })

  if (kpis.isLoading) return <Skeleton className="h-96" />
  if (!kpis.data || kpis.data.years.length === 0) {
    return (
      <Card>
        <EmptyState
          icon={ChartColumn}
          title="Sube tus estados financieros para empezar"
          description="Los KPIs se calculan solos al cargar el libro contable."
        />
      </Card>
    )
  }

  const years = [...kpis.data.years].sort((a, b) => a.year - b.year)
  const latest = years.at(-1)!
  const definitions = kpis.data.definitions.filter((d) =>
    years.some((y) => y.values[d.code] !== null),
  )

  return (
    <div className="space-y-6">
      <Card className="overflow-hidden" id="tabla-kpis">
        <Table>
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead>Indicador</TableHead>
              {years.map((y) => (
                <TableHead key={y.year} className="numeric">
                  {y.year}
                </TableHead>
              ))}
              <TableHead className="numeric">Variación {latest.year}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {definitions.map((d) => {
              const change = kpiChange(d.unit, latest.trends?.kpis[d.code])
              return (
                <TableRow key={d.code}>
                  <TableCell>
                    <span className="flex items-center gap-1 whitespace-nowrap">
                      {d.name}
                      <SimpleTooltip content={KPI_FORMULAS[d.code]}>
                        <button
                          type="button"
                          className="text-foreground-muted"
                          aria-label={`Fórmula de ${d.name}`}
                        >
                          <CircleHelp className="size-3.5" aria-hidden="true" />
                        </button>
                      </SimpleTooltip>
                    </span>
                  </TableCell>
                  {years.map((y) => (
                    <TableCell key={y.year} className="numeric whitespace-nowrap">
                      {formatKpi(y.values[d.code], d.unit, currency)}
                    </TableCell>
                  ))}
                  <TableCell className="numeric whitespace-nowrap">
                    <TrendIndicator
                      value={change.value}
                      unit={change.unit}
                      stableThreshold={change.stableThreshold}
                      higherIsBetter={d.higherIsBetter}
                    />
                  </TableCell>
                </TableRow>
              )
            })}
          </TableBody>
        </Table>
      </Card>

      <section className="space-y-3" aria-labelledby="kpi-charts">
        <h2 id="kpi-charts" className="text-h2 text-foreground">
          Evolución por KPI
        </h2>
        <div className="grid gap-3 sm:gap-4 md:grid-cols-2 xl:grid-cols-3">
          {definitions.map((d) => {
            const data = years.map((y) => ({ year: String(y.year), value: y.values[d.code] }))
            const format = (v: number) => formatKpi(v, d.unit, currency, true)
            return (
              <ChartCard
                key={d.code}
                title={d.name}
                subtitle={`${KPI_FORMULAS[d.code]} · ${years[0]!.year}–${latest.year}`}
                ariaLabel={`${d.name}: ${data.map((p) => `${p.year} ${p.value == null ? 'sin dato' : format(p.value)}`).join(', ')}`}
                table={{
                  columns: [{ label: 'Año' }, { label: d.name, numeric: true }],
                  rows: data.map((p) => [p.year, p.value == null ? '—' : format(p.value)]),
                }}
              >
                <TrendLineChart
                  data={data}
                  xKey="year"
                  series={[{ key: 'value', name: d.name, color: PRIMARY_SERIES }]}
                  formatValue={format}
                  height={160}
                />
              </ChartCard>
            )
          })}
        </div>
      </section>
    </div>
  )
}
