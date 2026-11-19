import type { KpiUnit } from '@/types/analysis'
import { formatCurrency, formatNumber, formatPercent, formatPp } from './format'

/** Etiquetas en español de los datos de la evidencia y del cálculo de impacto. */
const LABELS: Record<
  string,
  [string, 'money' | 'ratio' | 'percent' | 'pp' | 'number' | 'text' | 'times']
> = {
  previous: ['Valor anterior', 'ratio'],
  current: ['Valor actual', 'ratio'],
  changePp: ['Variación', 'pp'],
  changePercent: ['Variación', 'percent'],
  costGrowthPercent: ['Crecimiento del costo', 'percent'],
  revenueGrowthPercent: ['Crecimiento de los ingresos', 'percent'],
  gapPp: ['Diferencia', 'pp'],
  previousCost: ['Costo del año anterior', 'money'],
  currentCost: ['Costo del año', 'money'],
  previousRevenue: ['Ingresos del año anterior', 'money'],
  currentRevenue: ['Ingresos del año', 'money'],
  revenue: ['Ingresos', 'money'],
  benchmark: ['Referencia de la industria', 'ratio'],
  threshold: ['Umbral', 'ratio'],
  minimum: ['Mínimo recomendado', 'times'],
  maximum: ['Máximo recomendado', 'times'],
  reference: ['Referencia', 'times'],
  ratio: ['Proporción', 'ratio'],
  materialLoss: ['Pérdida de materiales', 'money'],
  cogs: ['Costo de ventas', 'money'],
  previousMargin: ['Margen anterior', 'ratio'],
  currentMargin: ['Margen actual', 'ratio'],
  previousRatio: ['Proporción anterior', 'ratio'],
  currentRatio: ['Proporción actual', 'ratio'],
  employees: ['Empleados', 'number'],
  previousRevenuePerEmployee: ['Ingresos por empleado (anterior)', 'money'],
  currentRevenuePerEmployee: ['Ingresos por empleado (actual)', 'money'],
  currentEnergyCost: ['Costo de energía del año', 'money'],
  previousEnergyCost: ['Costo de energía anterior', 'money'],
  flatBandPercent: ['Banda de ingresos estables', 'percent'],
}

/** Filas legibles de la evidencia (se omiten las claves técnicas como `kpi`, `year` o `impact`). */
export function evidenceRows(
  evidence: Record<string, unknown>,
  currency: string,
  relatedUnit?: KpiUnit,
): { label: string; value: string }[] {
  return Object.entries(evidence)
    .filter(
      ([key, value]) => key in LABELS && (typeof value === 'number' || typeof value === 'string'),
    )
    .map(([key, value]) => {
      const [label, kind] = LABELS[key]!
      const v = value as number
      const isKpiValue = key === 'previous' || key === 'current'
      const effective =
        isKpiValue && relatedUnit === 'Times'
          ? 'times'
          : isKpiValue && relatedUnit === 'Money'
            ? 'money'
            : kind
      const text =
        typeof value === 'string'
          ? value
          : effective === 'money'
            ? formatCurrency(v, currency)
            : effective === 'ratio'
              ? formatPercent(v)
              : effective === 'percent'
                ? `${formatNumber(v, 2)} %`
                : effective === 'pp'
                  ? formatPp(v)
                  : effective === 'times'
                    ? `${formatNumber(v, 2)}×`
                    : formatNumber(v)
      return { label, value: text }
    })
}
