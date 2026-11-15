import { describe, expect, it } from 'vitest'

import {
  EMPTY_VALUE,
  formatCurrency,
  formatDate,
  formatNumber,
  formatPercent,
  formatPp,
} from '@/lib/format'

// Intl usa espacios no separables; se normalizan para comparar.
const n = (s: string) => s.replace(/\s/gu, ' ')

describe('format', () => {
  it('formatea porcentajes con 1 decimal', () => {
    expect(n(formatPercent(0.124))).toBe('12,4 %')
  })

  it('formatea puntos porcentuales con signo', () => {
    expect(formatPp(-2.3)).toBe('−2,3 pp')
    expect(formatPp(1.5)).toBe('+1,5 pp')
  })

  it('formatea moneda', () => {
    expect(n(formatCurrency(1234.5, 'USD'))).toContain('1234,50')
    expect(n(formatCurrency(1_200_000, 'USD', { compact: true }))).toMatch(/1,2\sM/)
  })

  it('formatea números', () => {
    expect(n(formatNumber(12345.678, 1))).toBe('12.345,7')
  })

  it('formatea fechas como "15 nov 2026"', () => {
    expect(formatDate(new Date(2026, 10, 15))).toBe('15 nov 2026')
  })

  it('devuelve "—" para valores vacíos', () => {
    expect(formatPercent(null)).toBe(EMPTY_VALUE)
    expect(formatCurrency(undefined)).toBe(EMPTY_VALUE)
    expect(formatNumber(Number.NaN)).toBe(EMPTY_VALUE)
    expect(formatDate('no-es-fecha')).toBe(EMPTY_VALUE)
  })
})
