import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'

import { AXIS_LINE, AXIS_TICK, BAR_CURSOR, GRID_PROPS } from './chart-theme'
import { ChartLegend } from './ChartLegend'
import { ChartTooltip } from './ChartTooltip'
import type { LineSeries } from './TrendLineChart'

interface GroupedBarChartProps {
  data: Record<string, number | string | null>[]
  xKey: string
  series: LineSeries[]
  formatValue: (value: number) => string
  formatAxis?: (value: number) => string
  height?: number
  /** Barras apiladas (composición, GUIA §7): solo la última serie lleva las esquinas redondeadas. */
  stacked?: boolean
}

/**
 * Barras verticales agrupadas (comparar series por año) o apiladas (composición): esquinas de
 * 4 px y 2 px entre barras.
 */
export function GroupedBarChart({
  data,
  xKey,
  series,
  formatValue,
  formatAxis,
  height = 240,
  stacked = false,
}: GroupedBarChartProps) {
  return (
    <div className="space-y-2">
      <ChartLegend items={series.map((s) => ({ label: s.name, color: s.color }))} />
      <ResponsiveContainer width="100%" height={height}>
        <BarChart data={data} barGap={2} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
          <CartesianGrid {...GRID_PROPS} />
          <XAxis dataKey={xKey} tick={AXIS_TICK} tickLine={false} axisLine={AXIS_LINE} />
          <YAxis
            tick={AXIS_TICK}
            tickLine={false}
            axisLine={false}
            width={84}
            tickFormatter={(v: number) => (formatAxis ?? formatValue)(v)}
          />
          <Tooltip
            cursor={BAR_CURSOR}
            content={(props) => (
              <ChartTooltip
                active={props.active}
                payload={props.payload as never}
                label={props.label as string | number | undefined}
                formatValue={(v) => formatValue(v)}
              />
            )}
          />
          {series.map((s, index) => (
            <Bar
              key={s.key}
              dataKey={s.key}
              name={s.name}
              fill={s.color}
              stackId={stacked ? 'stack' : undefined}
              radius={!stacked || index === series.length - 1 ? [4, 4, 0, 0] : [0, 0, 0, 0]}
              maxBarSize={40}
              isAnimationActive={false}
            />
          ))}
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}
