/**
 * Única fuente de formato de datos de la interfaz (GUIA_DE_DISENO.md, sección 9).
 * Prohibido formatear números, monedas o fechas a mano en los componentes.
 */

export const LOCALE = 'es'
export const EMPTY_VALUE = '—'

type Maybe<T> = T | null | undefined

const isMissing = (value: Maybe<number>): value is null | undefined =>
  value === null || value === undefined || Number.isNaN(value)

const CURRENCY_SYMBOLS: Record<string, string> = { DOP: 'RD$', USD: 'US$', EUR: '€' }

/** Símbolo de la moneda (el mismo que usa el informe PDF): DOP → "RD$", USD → "US$". */
export function currencySymbol(currency: string): string {
  const code = currency.toUpperCase()
  return CURRENCY_SYMBOLS[code] ?? code
}

/** Moneda con el símbolo delante: "RD$ 4.397.941,56". `compact` para tarjetas: "RD$ 4,4 M". */
export function formatCurrency(
  value: Maybe<number>,
  currency = 'USD',
  options: { compact?: boolean } = {},
): string {
  if (isMissing(value)) return EMPTY_VALUE
  const number = new Intl.NumberFormat(LOCALE, {
    notation: options.compact ? 'compact' : 'standard',
    minimumFractionDigits: options.compact ? 0 : 2,
    maximumFractionDigits: options.compact ? 1 : 2,
  }).format(Math.abs(value))
  return `${value < 0 ? '−' : ''}${currencySymbol(currency)} ${number}`
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

const MONTHS = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic']

/**
 * Día de un valor "AAAA-MM-DD" sin zona horaria: "14 nov 2024". Con `withYear: false`, "14 nov".
 * (No usa `new Date(texto)`, que lo leería en UTC y podría mostrar el día anterior.)
 */
export function formatDay(value: Maybe<string>, options: { withYear?: boolean } = {}): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value ?? '')
  if (!match) return EMPTY_VALUE
  const [, year, month, day] = match
  const text = `${Number(day)} ${MONTHS[Number(month) - 1]}`
  return options.withYear === false ? text : `${text} ${year}`
}

/** Mes de un año: (2024, 11) → "nov 2024". Sin año: "nov". */
export function formatMonth(year: Maybe<number>, month: Maybe<number>): string {
  if (isMissing(month) || month < 1 || month > 12) return EMPTY_VALUE
  return isMissing(year) ? MONTHS[month - 1]! : `${MONTHS[month - 1]} ${year}`
}

/** Minutos: 1234 → "1.234 min". */
export function formatMinutes(value: Maybe<number>): string {
  if (isMissing(value)) return EMPTY_VALUE
  return `${formatNumber(value)} min`
}

/** Tamaño de archivo: "850 KB", "2,4 MB". */
export function formatFileSize(bytes: Maybe<number>): string {
  if (isMissing(bytes)) return EMPTY_VALUE
  if (bytes < 1024 * 1024) return `${formatNumber(Math.max(1, Math.round(bytes / 1024)))} KB`
  return `${formatNumber(bytes / (1024 * 1024), 1)} MB`
}

/** Lista de años: [2022, 2023, 2024] → "2022, 2023 y 2024". */
export function formatYears(years: readonly number[]): string {
  if (years.length === 0) return EMPTY_VALUE
  const sorted = [...years].sort((a, b) => a - b).map(String)
  if (sorted.length === 1) return sorted[0]!
  return `${sorted.slice(0, -1).join(', ')} y ${sorted.at(-1)}`
}
