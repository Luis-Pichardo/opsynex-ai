import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'

import { AXIS_LINE, AXIS_TICK, BAR_CURSOR, PRIMARY_SERIES } from './chart-theme'
import { ChartTooltip } from './ChartTooltip'

interface HorizontalBarChartProps {
  data: { label: string; value: number }[]
  /** Nombre de la medida (para el tooltip). */
  valueName: string
  formatValue: (value: number) => string
  formatAxis?: (value: number) => string
  color?: string
}

/** Barras horizontales ordenadas de mayor a menor (comparación entre categorías, GUIA §7). */
export function HorizontalBarChart({
  data,
  valueName,
  formatValue,
  formatAxis,
  color = PRIMARY_SERIES,
}: HorizontalBarChartProps) {
  const sorted = [...data].sort((a, b) => b.value - a.value)
  return (
    <ResponsiveContainer width="100%" height={Math.max(120, sorted.length * 44 + 32)}>
      <BarChart data={sorted} layout="vertical" margin={{ top: 0, right: 16, bottom: 0, left: 0 }}>
        <CartesianGrid stroke="var(--chart-grid)" horizontal={false} vertical />
        <XAxis
          type="number"
          tick={AXIS_TICK}
          tickLine={false}
          axisLine={AXIS_LINE}
          tickFormatter={(v: number) => (formatAxis ?? formatValue)(v)}
        />
        <YAxis
          type="category"
          dataKey="label"
          tick={AXIS_TICK}
          tickLine={false}
          axisLine={false}
          width={140}
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
        <Bar
          dataKey="value"
          name={valueName}
          fill={color}
          radius={[0, 4, 4, 0]}
          maxBarSize={24}
          isAnimationActive={false}
        />
      </BarChart>
    </ResponsiveContainer>
  )
}
