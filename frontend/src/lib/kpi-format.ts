import type { KpiUnit } from '@/types/analysis'
import { EMPTY_VALUE, formatCurrency, formatNumber, formatPercent } from './format'

/** Valor de un KPI según su unidad (proporción, veces, monto o días). */
export function formatKpi(
  value: number | null | undefined,
  unit: KpiUnit,
  currency: string,
  compact = false,
): string {
  if (value === null || value === undefined || Number.isNaN(value)) return EMPTY_VALUE
  switch (unit) {
    case 'Ratio':
      return formatPercent(value)
    case 'Times':
      return `${formatNumber(value, 2)}×`
    case 'Money':
      return formatCurrency(value, currency, { compact })
    case 'Days':
      return `${formatNumber(value)} días`
  }
}

/**
 * Variación para `TrendIndicator`: en puntos porcentuales para las proporciones (márgenes y
 * ratios) y en % para el resto.
 */
export function kpiChange(
  unit: KpiUnit,
  trend: { change: number | null; changePercent: number | null } | undefined | null,
): { value: number | null; unit: 'pp' | 'percent'; stableThreshold: number } {
  if (unit === 'Ratio') {
    return {
      value: trend?.change == null ? null : trend.change * 100,
      unit: 'pp',
      stableThreshold: 0.1,
    }
  }
  return { value: trend?.changePercent ?? null, unit: 'percent', stableThreshold: 1 }
}
