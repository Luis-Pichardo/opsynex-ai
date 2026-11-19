import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'

import { AXIS_LINE, AXIS_TICK, GRID_PROPS, LINE_CURSOR } from './chart-theme'
import { ChartLegend } from './ChartLegend'
import { ChartTooltip } from './ChartTooltip'

export interface LineSeries {
  key: string
  name: string
  color: string
}

interface TrendLineChartProps {
  data: Record<string, number | string | null>[]
  xKey: string
  series: LineSeries[]
  formatValue: (value: number) => string
  /** Formato corto para el eje Y. */
  formatAxis?: (value: number) => string
  height?: number
}

/** Líneas de 2 px por año; punto de 8 px al pasar el cursor y en el último valor (GUIA §7). */
export function TrendLineChart({
  data,
  xKey,
  series,
  formatValue,
  formatAxis,
  height = 240,
}: TrendLineChartProps) {
  const lastIndex = data.length - 1
  return (
    <div className="space-y-2">
      <ChartLegend items={series.map((s) => ({ label: s.name, color: s.color }))} />
      <ResponsiveContainer width="100%" height={height}>
        <LineChart data={data} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
          <CartesianGrid {...GRID_PROPS} />
          <XAxis dataKey={xKey} tick={AXIS_TICK} tickLine={false} axisLine={AXIS_LINE} />
          <YAxis
            tick={AXIS_TICK}
            tickLine={false}
            axisLine={false}
            width={72}
            tickFormatter={(v: number) => (formatAxis ?? formatValue)(v)}
          />
          <Tooltip
            cursor={LINE_CURSOR}
            content={(props) => (
              <ChartTooltip
                active={props.active}
                payload={props.payload as never}
                label={props.label as string | number | undefined}
                formatValue={(v) => formatValue(v)}
              />
            )}
          />
          {series.map((s) => (
            <Line
              key={s.key}
              type="monotone"
              dataKey={s.key}
              name={s.name}
              stroke={s.color}
              strokeWidth={2}
              connectNulls
              isAnimationActive={false}
              activeDot={{ r: 4, fill: s.color, strokeWidth: 0 }}
              dot={(props: { cx?: number; cy?: number; index?: number }) =>
                props.index === lastIndex && props.cx !== undefined && props.cy !== undefined ? (
                  <circle key={`${s.key}-last`} cx={props.cx} cy={props.cy} r={4} fill={s.color} />
                ) : (
                  <g key={`${s.key}-${props.index}`} />
                )
              }
            />
          ))}
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}
