import { ChartCard } from '@/components/charts/ChartCard'
import { HorizontalBarChart } from '@/components/charts/HorizontalBarChart'
import { INDUSTRIAL_SERIES } from '@/components/charts/chart-theme'
import { TrendLineChart } from '@/components/charts/TrendLineChart'
import { PageHeader } from '@/components/common/PageHeader'
import { RequireCompany } from '@/components/common/RequireCompany'
import { StatCard } from '@/components/common/StatCard'
import { IndustrialPage } from '@/components/industrial/IndustrialPage'
import { IndustrialScoreCard } from '@/components/industrial/IndustrialScoreCard'
import { LossesCard } from '@/components/industrial/LossesCard'
import { MachineRanking } from '@/components/industrial/MachineRanking'
import { ThresholdStatus } from '@/components/industrial/ThresholdStatus'
import { useIndustrialScore } from '@/industrial/useIndustrial'
import { scopeLabel } from '@/lib/industrial'
import { formatDay, formatMinutes, formatMonth, formatNumber, formatPercent } from '@/lib/format'
import type { IndustrialFilter, IndustrialKpisResponse } from '@/types/industrial'

/** Producción (módulo industrial): OEE con sus 3 componentes, paradas por máquina y ranking. */
export function ProductionPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Producción"
        description="OEE, disponibilidad, rendimiento, calidad, scrap y paradas por máquina y línea."
      />
      <RequireCompany>
        <IndustrialPage render={(data, filter) => <Production data={data} filter={filter} />} />
      </RequireCompany>
    </div>
  )
}

function Production({ data, filter }: { data: IndustrialKpisResponse; filter: IndustrialFilter }) {
  const score = useIndustrialScore(filter)
  const { summary, thresholds, currency } = data
  const { scope, period } = scopeLabel(data)
  const kpis = summary?.kpis

  const monthly = data.monthly.map((m) => ({
    month: formatMonth(null, m.month),
    oee: m.kpis.oee,
    availability: m.kpis.availability,
    performance: m.kpis.performance,
    quality: m.kpis.quality,
  }))
  const daily = data.daily.map((d) => ({
    day: formatDay(d.date, { withYear: false }),
    oee: d.oee,
    downtime: d.downtimeMinutes,
  }))
  const downtime = data.byMachine.map((m) => ({
    label: m.machineId ?? '—',
    value: m.downtimeMinutes,
  }))
  const pct = (v: number) => formatPercent(v)
  const pctAxis = (v: number) => formatPercent(v, 0)

  return (
    <>
      <section
        className="grid gap-3 sm:gap-4 lg:grid-cols-12"
        aria-label="Score industrial y pérdidas"
      >
        <div className="lg:col-span-5">
          <IndustrialScoreCard
            score={score.data}
            loading={score.isLoading}
            scope={`${scope} · ${period}`}
          />
        </div>
        <div className="lg:col-span-7">
          <LossesCard losses={summary?.losses} currency={currency} period={period} />
        </div>
      </section>

      <section
        className="grid gap-3 sm:grid-cols-2 sm:gap-4 lg:grid-cols-3 2xl:grid-cols-6"
        aria-label="Indicadores de producción"
      >
        <StatCard
          label="OEE"
          hint="Disponibilidad × Rendimiento × Calidad"
          value={formatPercent(kpis?.oee)}
        >
          <ThresholdStatus value={kpis?.oee} threshold={thresholds.lowOee} higherIsBetter />
        </StatCard>
        <StatCard
          label="Disponibilidad"
          hint="(Tiempo planificado − paradas) / tiempo planificado"
          value={formatPercent(kpis?.availability)}
        />
        <StatCard
          label="Rendimiento"
          hint="(Ciclo estándar × unidades totales) / tiempo en marcha"
          value={formatPercent(kpis?.performance)}
        />
        <StatCard
          label="Calidad"
          hint="Unidades buenas / unidades totales"
          value={formatPercent(kpis?.quality)}
        />
        <StatCard
          label="Scrap"
          hint="(Unidades totales − buenas) / unidades totales"
          value={formatPercent(kpis?.scrapRate)}
        >
          <ThresholdStatus
            value={kpis?.scrapRate}
            threshold={thresholds.highScrap}
            higherIsBetter={false}
          />
        </StatCard>
        <StatCard
          label="Paradas"
          hint="Minutos de parada / tiempo planificado"
          value={formatPercent(kpis?.downtimeRatio)}
        >
          <span className="text-small text-foreground-secondary">
            {formatMinutes(summary?.downtimeMinutes)} · {formatNumber(summary?.days)} días
          </span>
          <ThresholdStatus
            value={kpis?.downtimeRatio}
            threshold={thresholds.highDowntime}
            higherIsBetter={false}
          />
        </StatCard>
      </section>

      <section className="grid gap-3 sm:gap-4 lg:grid-cols-2" aria-label="Gráficos de producción">
        <ChartCard
          title="OEE y sus componentes por mes"
          subtitle={`Porcentaje · ${scope} · ${data.year}`}
          ariaLabel={`OEE por mes: ${monthly.map((m) => `${m.month} ${m.oee == null ? 'sin dato' : pct(m.oee)}`).join(', ')}`}
          table={{
            columns: [
              { label: 'Mes' },
              { label: 'OEE', numeric: true },
              { label: 'Disponibilidad', numeric: true },
              { label: 'Rendimiento', numeric: true },
              { label: 'Calidad', numeric: true },
            ],
            rows: data.monthly.map((m) => [
              formatMonth(m.year, m.month),
              formatPercent(m.kpis.oee),
              formatPercent(m.kpis.availability),
              formatPercent(m.kpis.performance),
              formatPercent(m.kpis.quality),
            ]),
          }}
        >
          <TrendLineChart
            data={monthly}
            xKey="month"
            series={[
              { key: 'oee', name: 'OEE', color: INDUSTRIAL_SERIES.oee },
              {
                key: 'availability',
                name: 'Disponibilidad',
                color: INDUSTRIAL_SERIES.availability,
              },
              { key: 'performance', name: 'Rendimiento', color: INDUSTRIAL_SERIES.performance },
              { key: 'quality', name: 'Calidad', color: INDUSTRIAL_SERIES.quality },
            ]}
            formatValue={pct}
            formatAxis={pctAxis}
          />
        </ChartCard>
        <ChartCard
          title="Paradas por máquina"
          subtitle={`Minutos de parada · ${period}`}
          ariaLabel={`Paradas por máquina: ${downtime.map((d) => `${d.label} ${formatMinutes(d.value)}`).join(', ')}`}
          table={{
            columns: [
              { label: 'Máquina' },
              { label: 'Minutos', numeric: true },
              { label: '% del tiempo', numeric: true },
            ],
            rows: data.byMachine.map((m) => [
              m.machineId ?? '—',
              formatNumber(m.downtimeMinutes),
              formatPercent(m.kpis.downtimeRatio),
            ]),
          }}
        >
          <HorizontalBarChart
            data={downtime}
            valueName="Paradas"
            formatValue={formatMinutes}
            formatAxis={(v) => formatNumber(v)}
          />
        </ChartCard>
        <ChartCard
          title="OEE diario"
          subtitle={`Porcentaje por día · ${scope} · ${period} · mínimo ${formatPercent(thresholds.lowOee, 0)}`}
          ariaLabel={`OEE diario de ${daily.length} días`}
          table={{
            columns: [{ label: 'Día' }, { label: 'OEE', numeric: true }],
            rows: data.daily.map((d) => [formatDay(d.date), formatPercent(d.oee)]),
          }}
        >
          <TrendLineChart
            data={daily}
            xKey="day"
            series={[{ key: 'oee', name: 'OEE', color: INDUSTRIAL_SERIES.oee }]}
            formatValue={pct}
            formatAxis={pctAxis}
          />
        </ChartCard>
        <ChartCard
          title="Paradas diarias"
          subtitle={`Minutos por día · ${scope} · ${period}`}
          ariaLabel={`Paradas diarias de ${daily.length} días`}
          table={{
            columns: [{ label: 'Día' }, { label: 'Minutos', numeric: true }],
            rows: data.daily.map((d) => [formatDay(d.date), formatNumber(d.downtimeMinutes)]),
          }}
        >
          <TrendLineChart
            data={daily}
            xKey="day"
            series={[{ key: 'downtime', name: 'Paradas', color: INDUSTRIAL_SERIES.downtime }]}
            formatValue={formatMinutes}
            formatAxis={(v) => formatNumber(v)}
          />
        </ChartCard>
      </section>

      <MachineRanking rows={data.byMachine} thresholds={thresholds} currency={currency} />
    </>
  )
}
