import { Coins } from 'lucide-react'

import { ChartCard } from '@/components/charts/ChartCard'
import { INDUSTRIAL_SERIES } from '@/components/charts/chart-theme'
import { GroupedBarChart } from '@/components/charts/GroupedBarChart'
import { EmptyState } from '@/components/common/EmptyState'
import { PageHeader } from '@/components/common/PageHeader'
import { RequireCompany } from '@/components/common/RequireCompany'
import { StatCard } from '@/components/common/StatCard'
import { IndustrialPage } from '@/components/industrial/IndustrialPage'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { formatCurrency, formatMonth, formatNumber, formatPercent } from '@/lib/format'
import { scopeLabel } from '@/lib/industrial'
import type { IndustrialKpisResponse, IndustrialRow } from '@/types/industrial'

/** Costos (módulo industrial): costo por unidad frente al estándar y composición del costo. */
export function CostsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Costos"
        description="Costo por unidad frente al estándar y composición del costo por máquina."
      />
      <RequireCompany>
        <IndustrialPage render={(data) => <Costs data={data} />} />
      </RequireCompany>
    </div>
  )
}

/** Desviación del costo por unidad frente al estándar (0,18 = 18 % por encima). */
function deviation(row: IndustrialRow | null | undefined): number | null {
  const actual = row?.kpis.costPerUnit
  const standard = row?.kpis.standardCostPerUnit
  return actual != null && standard ? actual / standard - 1 : null
}

function DeviationBadge({ value, tolerance }: { value: number | null; tolerance: number }) {
  if (value === null) return <span className="text-foreground-muted">—</span>
  if (value > tolerance) {
    return <Badge variant="critical">+{formatPercent(value)} sobre el estándar</Badge>
  }
  if (value > 0) {
    return (
      <Badge variant="neutral">
        +{formatPercent(value)} (tolerancia {formatPercent(tolerance, 0)})
      </Badge>
    )
  }
  return <Badge variant="good">{formatPercent(value)} vs. estándar</Badge>
}

function Costs({ data }: { data: IndustrialKpisResponse }) {
  const { summary, currency, thresholds } = data
  const { scope, period } = scopeLabel(data)
  const money = (v: number) => formatCurrency(v, currency)
  const moneyAxis = (v: number) => formatCurrency(v, currency, { compact: true })

  if (!summary?.costs) {
    return (
      <Card>
        <EmptyState
          icon={Coins}
          title="Faltan los costos de producción"
          description="Sube la plantilla de costos por máquina (mano de obra, materiales, energía y gastos generales) y la de estándares para comparar el costo por unidad."
        />
      </Card>
    )
  }

  const totalDeviation = deviation(summary)
  const perMachine = data.byMachine.map((m) => ({
    machine: m.machineId ?? '—',
    actual: m.kpis.costPerUnit,
    standard: m.kpis.standardCostPerUnit,
  }))
  const composition = data.monthly.map((m) => ({
    month: formatMonth(null, m.month),
    labor: m.costs?.labor ?? null,
    material: m.costs?.material ?? null,
    energy: m.costs?.energy ?? null,
    overhead: m.costs?.overhead ?? null,
  }))
  const costSeries = [
    { key: 'labor', name: 'Mano de obra', color: INDUSTRIAL_SERIES.labor },
    { key: 'material', name: 'Materiales', color: INDUSTRIAL_SERIES.material },
    { key: 'energy', name: 'Energía', color: INDUSTRIAL_SERIES.energy },
    { key: 'overhead', name: 'Gastos generales', color: INDUSTRIAL_SERIES.overhead },
  ]

  return (
    <>
      <section
        className="grid gap-3 sm:grid-cols-2 sm:gap-4 xl:grid-cols-4"
        aria-label="Indicadores de costos"
      >
        <StatCard
          label="Costo por unidad"
          hint="Costo total / unidades producidas"
          value={formatCurrency(summary.kpis.costPerUnit, currency)}
        >
          <DeviationBadge value={totalDeviation} tolerance={thresholds.costTolerance} />
        </StatCard>
        <StatCard
          label="Costo estándar"
          hint="Costo estándar por unidad ponderado por las unidades producidas"
          value={formatCurrency(summary.kpis.standardCostPerUnit, currency)}
        />
        <StatCard
          label="Costo total"
          hint="Mano de obra + materiales + energía + gastos generales"
          value={formatCurrency(summary.costs.total, currency, { compact: true })}
        >
          <span className="text-small text-foreground-secondary">
            {formatNumber(summary.unitsProduced)} unidades producidas
          </span>
        </StatCard>
        <StatCard
          label="Capacidad utilizada"
          hint="(Ciclo estándar × unidades totales) / tiempo planificado"
          value={formatPercent(summary.kpis.capacityUtilization)}
        />
      </section>

      <section className="grid gap-3 sm:gap-4 lg:grid-cols-2" aria-label="Gráficos de costos">
        <ChartCard
          title="Costo por unidad frente al estándar"
          subtitle={`${currency} por unidad · ${period}`}
          ariaLabel={`Costo por unidad por máquina: ${perMachine.map((m) => `${m.machine} ${m.actual == null ? 'sin dato' : money(m.actual)} frente a ${m.standard == null ? 'sin estándar' : money(m.standard)}`).join(', ')}`}
          table={{
            columns: [
              { label: 'Máquina' },
              { label: 'Real', numeric: true },
              { label: 'Estándar', numeric: true },
            ],
            rows: perMachine.map((m) => [
              m.machine,
              formatCurrency(m.actual, currency),
              formatCurrency(m.standard, currency),
            ]),
          }}
        >
          <GroupedBarChart
            data={perMachine}
            xKey="machine"
            series={[
              { key: 'actual', name: 'Costo real', color: INDUSTRIAL_SERIES.actualCost },
              { key: 'standard', name: 'Costo estándar', color: INDUSTRIAL_SERIES.standardCost },
            ]}
            formatValue={money}
            formatAxis={moneyAxis}
          />
        </ChartCard>
        <ChartCard
          title="Composición del costo por mes"
          subtitle={`${currency} · ${scope} · ${data.year}`}
          ariaLabel={`Composición del costo por mes: ${data.monthly.map((m) => `${formatMonth(m.year, m.month)} ${formatCurrency(m.costs?.total, currency)}`).join(', ')}`}
          table={{
            columns: [
              { label: 'Mes' },
              ...costSeries.map((s) => ({ label: s.name, numeric: true })),
              { label: 'Total', numeric: true },
            ],
            rows: data.monthly.map((m) => [
              formatMonth(m.year, m.month),
              formatCurrency(m.costs?.labor, currency),
              formatCurrency(m.costs?.material, currency),
              formatCurrency(m.costs?.energy, currency),
              formatCurrency(m.costs?.overhead, currency),
              formatCurrency(m.costs?.total, currency),
            ]),
          }}
        >
          <GroupedBarChart
            data={composition}
            xKey="month"
            series={costSeries}
            formatValue={money}
            formatAxis={moneyAxis}
            stacked
          />
        </ChartCard>
      </section>

      <Card className="overflow-hidden">
        <div className="space-y-0.5 p-4 sm:p-5">
          <h2 className="text-h3 text-foreground">Costos por máquina</h2>
          <p className="text-small text-foreground-secondary">
            {period}. Un costo más de {formatPercent(thresholds.costTolerance, 0)} sobre el estándar
            genera el problema «Costo por unidad sobre el estándar».
          </p>
        </div>
        <Table aria-label="Costos por máquina">
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead>Máquina</TableHead>
              <TableHead className="numeric">Unidades</TableHead>
              <TableHead className="numeric">Costo total</TableHead>
              <TableHead className="numeric">Costo por unidad</TableHead>
              <TableHead className="numeric">Estándar</TableHead>
              <TableHead>Desviación</TableHead>
              <TableHead className="numeric">Mano de obra</TableHead>
              <TableHead className="numeric">Materiales</TableHead>
              <TableHead className="numeric">Energía</TableHead>
              <TableHead className="numeric">Gastos generales</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.byMachine.map((row) => (
              <TableRow key={row.machineId}>
                <TableCell className="whitespace-nowrap font-medium text-foreground">
                  {row.machineId}
                </TableCell>
                <TableCell className="numeric">{formatNumber(row.unitsProduced)}</TableCell>
                <TableCell className="numeric whitespace-nowrap">
                  {formatCurrency(row.costs?.total, currency, { compact: true })}
                </TableCell>
                <TableCell className="numeric whitespace-nowrap">
                  {formatCurrency(row.kpis.costPerUnit, currency)}
                </TableCell>
                <TableCell className="numeric whitespace-nowrap">
                  {formatCurrency(row.kpis.standardCostPerUnit, currency)}
                </TableCell>
                <TableCell className="whitespace-nowrap">
                  <DeviationBadge value={deviation(row)} tolerance={thresholds.costTolerance} />
                </TableCell>
                {(['labor', 'material', 'energy', 'overhead'] as const).map((key) => (
                  <TableCell key={key} className="numeric whitespace-nowrap">
                    {formatCurrency(row.costs?.[key], currency, { compact: true })}
                  </TableCell>
                ))}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
    </>
  )
}
