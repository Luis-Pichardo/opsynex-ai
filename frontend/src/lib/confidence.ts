/** Nivel de confianza de una clasificación de cuenta (umbral de "dudosa": 0,8, igual que el API). */
export type ConfidenceLevel = 'high' | 'medium' | 'low'

export function confidenceLevel(confidence: number): ConfidenceLevel {
  if (confidence >= 0.8) return 'high'
  if (confidence >= 0.5) return 'medium'
  return 'low'
}

export const CONFIDENCE = {
  high: { label: 'Alta', variant: 'good' },
  medium: { label: 'Media', variant: 'warning' },
  low: { label: 'Baja', variant: 'critical' },
} as const
