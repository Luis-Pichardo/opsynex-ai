import { describe, expect, it } from 'vitest'

import {
  EMPTY_VALUE,
  formatCurrency,
  formatDate,
  formatDay,
  formatMinutes,
  formatMonth,
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
    expect(n(formatCurrency(1234.5, 'USD'))).toBe('US$ 1234,50')
    expect(n(formatCurrency(4_397_941.56, 'DOP'))).toBe('RD$ 4.397.941,56')
    expect(n(formatCurrency(1_200_000, 'USD', { compact: true }))).toMatch(/^US\$ 1,2\sM$/)
    expect(n(formatCurrency(-350_000, 'DOP', { compact: true }))).toMatch(/^−RD\$ 350\smil$/)
    expect(n(formatCurrency(10, 'EUR'))).toBe('€ 10,00')
  })

  it('formatea números', () => {
    expect(n(formatNumber(12345.678, 1))).toBe('12.345,7')
  })

  it('formatea fechas como "15 nov 2026"', () => {
    expect(formatDate(new Date(2026, 10, 15))).toBe('15 nov 2026')
  })

  it('formatea días, meses y minutos del módulo industrial sin zona horaria', () => {
    expect(formatDay('2024-10-01')).toBe('1 oct 2024')
    expect(formatDay('2024-11-14', { withYear: false })).toBe('14 nov')
    expect(formatDay('no es fecha')).toBe('—')
    expect(formatMonth(2024, 12)).toBe('dic 2024')
    expect(formatMonth(null, 1)).toBe('ene')
    expect(formatMonth(2024, 13)).toBe('—')
    expect(formatMinutes(27626)).toBe('27.626 min')
    expect(formatMinutes(null)).toBe('—')
  })

  it('devuelve "—" para valores vacíos', () => {
    expect(formatPercent(null)).toBe(EMPTY_VALUE)
    expect(formatCurrency(undefined)).toBe(EMPTY_VALUE)
    expect(formatNumber(Number.NaN)).toBe(EMPTY_VALUE)
    expect(formatDate('no-es-fecha')).toBe(EMPTY_VALUE)
  })
})
