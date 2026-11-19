/** Clasificación de una variación (GUIA_DE_DISENO.md §2.5). */
export type TrendKind = 'favorable' | 'unfavorable' | 'stable'

export function trendKind(value: number, higherIsBetter: boolean, stableThreshold = 1): TrendKind {
  if (Math.abs(value) < stableThreshold) return 'stable'
  return value > 0 === higherIsBetter ? 'favorable' : 'unfavorable'
}
