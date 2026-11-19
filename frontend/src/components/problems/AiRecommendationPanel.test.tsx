import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { AiRecommendationPanel } from './AiRecommendationPanel'

const RECOMMENDATION = {
  summary: 'El margen EBITDA cayó 1,14 pp por el costo de ventas.',
  rootCauses: ['Costo de materias primas al alza', 'Precios sin ajustar'],
  actions: [
    {
      title: 'Renegocia con los 3 principales proveedores',
      description: 'Busca descuentos por volumen.',
      expectedImpact: 'Recuperar 0,5 pp de margen.',
      effort: 'medium' as const,
      horizon: 'short' as const,
    },
  ],
  model: 'claude-opus-5-5',
  generatedAt: '2026-11-19T10:00:00Z',
  inputHash: 'x'.repeat(64),
}

describe('AiRecommendationPanel', () => {
  it('muestra diagnóstico, causas raíz y acciones con esfuerzo y horizonte', () => {
    render(<AiRecommendationPanel recommendation={RECOMMENDATION} severity="Medium" />)
    expect(screen.getByText('Generado con IA')).toBeInTheDocument()
    expect(screen.getByText(RECOMMENDATION.summary)).toBeInTheDocument()
    expect(screen.getByText('Costo de materias primas al alza')).toBeInTheDocument()
    expect(screen.getByText('1. Renegocia con los 3 principales proveedores')).toBeInTheDocument()
    expect(screen.getByText('Esfuerzo medio')).toBeInTheDocument()
    expect(screen.getByText('Corto plazo (menos de 3 meses)')).toBeInTheDocument()
    expect(screen.getByText(/orientativas/)).toBeInTheDocument()
  })

  it('sin recomendación explica cuándo la genera la IA', () => {
    render(<AiRecommendationPanel recommendation={null} severity="Low" />)
    expect(screen.getByText('Sin recomendación de IA')).toBeInTheDocument()
    expect(screen.getByText(/severidad alta y media/)).toBeInTheDocument()
  })
})
