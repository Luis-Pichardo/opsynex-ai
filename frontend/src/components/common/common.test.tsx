import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { scoreBand } from '@/lib/score'
import { trendKind } from '@/lib/trend'
import { ScoreGauge } from './ScoreGauge'
import { SeverityBadge } from './SeverityBadge'
import { TrendIndicator } from './TrendIndicator'

describe('scoreBand', () => {
  it.each([
    [100, 'Saludable'],
    [80, 'Saludable'],
    [79.9, 'Mejorable'],
    [60, 'Mejorable'],
    [59.9, 'Deterioro'],
  ])('%s → %s', (score, label) => expect(scoreBand(score).label).toBe(label))
})

describe('trendKind', () => {
  it('el color depende de si la variación es favorable, no de la dirección', () => {
    expect(trendKind(5, true)).toBe('favorable')
    expect(trendKind(-3.2, false)).toBe('favorable') // bajar el costo es bueno
    expect(trendKind(-3.2, true)).toBe('unfavorable')
    expect(trendKind(0.5, true)).toBe('stable')
  })
})

describe('componentes comunes', () => {
  it('TrendIndicator explica dirección y significado sin depender del color', () => {
    render(<TrendIndicator value={-3.2} higherIsBetter={false} />)
    expect(screen.getByText(/baja, favorable/)).toBeInTheDocument()
    expect(screen.getByText(/3,2/)).toBeInTheDocument()
  })

  it('ScoreGauge muestra el número y la etiqueta del rango', () => {
    render(<ScoreGauge score={83.24} />)
    expect(screen.getByRole('img')).toHaveAccessibleName(
      'Score Opsynex 83 de 100: Operación saludable',
    )
    expect(screen.getByText('Saludable')).toBeInTheDocument()
  })

  it('ScoreGauge sin datos', () => {
    render(<ScoreGauge score={null} />)
    expect(screen.getByText('Sin datos')).toBeInTheDocument()
  })

  it('SeverityBadge usa palabra e icono', () => {
    const { container } = render(<SeverityBadge severity="High" />)
    expect(screen.getByText('Alta')).toBeInTheDocument()
    expect(container.querySelector('svg')).not.toBeNull()
  })
})
