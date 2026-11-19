/** Bandas del Score Opsynex (GUIA_DE_DISENO.md §2.3). */
export type ScoreBand = 'good' | 'warning' | 'critical'

export interface ScoreBandInfo {
  band: ScoreBand
  label: string
  interpretation: string
}

export function scoreBand(score: number): ScoreBandInfo {
  if (score >= 80)
    return { band: 'good', label: 'Saludable', interpretation: 'Operación saludable' }
  if (score >= 60)
    return { band: 'warning', label: 'Mejorable', interpretation: 'Oportunidades de mejora' }
  return {
    band: 'critical',
    label: 'Deterioro',
    interpretation: 'Deterioro operacional significativo',
  }
}
