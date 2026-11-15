/**
 * Única fuente de formato de datos de la interfaz (GUIA_DE_DISENO.md, sección 9).
 * Prohibido formatear números, monedas o fechas a mano en los componentes.
 */

export const LOCALE = 'es'
export const EMPTY_VALUE = '—'

type Maybe<T> = T | null | undefined

const isMissing = (value: Maybe<number>): value is null | undefined =>
  value === null || value === undefined || Number.isNaN(value)

/** Moneda. `compact` para tarjetas: "1,2 M US$". */
export function formatCurrency(
  value: Maybe<number>,
  currency = 'USD',
  options: { compact?: boolean } = {},
): string {
  if (isMissing(value)) return EMPTY_VALUE
  return new Intl.NumberFormat(LOCALE, {
    style: 'currency',
    currency,
    notation: options.compact ? 'compact' : 'standard',
    maximumFractionDigits: options.compact ? 1 : 2,
  }).format(value)
}

/** Porcentaje con 1 decimal a partir de un ratio: 0.124 → "12,4 %". */
export function formatPercent(ratio: Maybe<number>, fractionDigits = 1): string {
  if (isMissing(ratio)) return EMPTY_VALUE
  return new Intl.NumberFormat(LOCALE, {
    style: 'percent',
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  }).format(ratio)
}

/** Variación en puntos porcentuales con signo: -2.3 → "−2,3 pp". */
export function formatPp(points: Maybe<number>): string {
  if (isMissing(points)) return EMPTY_VALUE
  const formatted = new Intl.NumberFormat(LOCALE, {
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
    signDisplay: 'exceptZero',
  }).format(points)
  return `${formatted.replace('-', '−')} pp`
}

/** Número general. */
export function formatNumber(value: Maybe<number>, fractionDigits = 0): string {
  if (isMissing(value)) return EMPTY_VALUE
  return new Intl.NumberFormat(LOCALE, {
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  }).format(value)
}

/** Fecha corta: "15 nov 2026". `withTime` agrega la hora (HH:mm). */
export function formatDate(
  value: Maybe<Date | string>,
  options: { withTime?: boolean } = {},
): string {
  if (value === null || value === undefined) return EMPTY_VALUE
  const date = typeof value === 'string' ? new Date(value) : value
  if (Number.isNaN(date.getTime())) return EMPTY_VALUE
  const parts = new Intl.DateTimeFormat(LOCALE, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  })
    .format(date)
    .replace(/\./g, '')
    .replace(/ de /g, ' ')
  if (!options.withTime) return parts
  const time = new Intl.DateTimeFormat(LOCALE, { hour: '2-digit', minute: '2-digit' }).format(date)
  return `${parts}, ${time}`
}
