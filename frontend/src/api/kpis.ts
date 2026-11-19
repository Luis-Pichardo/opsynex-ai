import type { KpiYearsResponse } from '@/types/api'
import type { KpisResponse } from '@/types/analysis'
import { apiRequest } from './client'

export const kpisApi = {
  /** Años con KPIs de la empresa actual (para el selector de año). */
  years: (signal?: AbortSignal) => apiRequest<KpiYearsResponse>('/kpis', { signal }),
  /** KPIs con tendencias y normalización: sin `year`, la serie histórica. */
  get: (year?: number | null, signal?: AbortSignal) =>
    apiRequest<KpisResponse>('/kpis', { query: { year: year ?? undefined }, signal }),
}
