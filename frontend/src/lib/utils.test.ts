import { describe, expect, it } from 'vitest'

import { cn, initials } from './utils'

describe('cn', () => {
  it('conserva el color del texto junto a un tamaño de la escala tipográfica', () => {
    const classes = cn('text-primary-foreground', 'text-body')
    expect(classes).toContain('text-primary-foreground')
    expect(classes).toContain('text-body')
  })

  it('resuelve conflictos entre tamaños de la escala', () => {
    expect(cn('text-body', 'text-small')).toBe('text-small')
  })
})

describe('initials', () => {
  it('usa la primera letra del nombre y del apellido', () => {
    expect(initials('Ana María Pérez')).toBe('AP')
    expect(initials('carla')).toBe('C')
  })
})
