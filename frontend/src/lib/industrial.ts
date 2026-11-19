import { formatMonth } from '@/lib/format'
import type { IndustrialKpisResponse } from '@/types/industrial'

/** Alcance y período de la vista: "Planta · todo 2024", "MAQ-03 · nov 2024". */
export function scopeLabel(data: IndustrialKpisResponse): { scope: string; period: string } {
  const scope = data.machineId ?? data.lineId ?? 'Planta'
  const period = data.month ? formatMonth(data.year, data.month) : `todo ${data.year}`
  return { scope, period }
}
