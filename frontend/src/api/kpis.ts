import type { KpiYearsResponse } from '@/types/api'
import { apiRequest } from './client'

export const kpisApi = {
  /** Años con KPIs de la empresa actual (el detalle de KPIs llega en la etapa 10). */
  years: (signal?: AbortSignal) => apiRequest<KpiYearsResponse>('/kpis', { signal }),
}
