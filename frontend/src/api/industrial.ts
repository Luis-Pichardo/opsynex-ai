import type { IndustrialFilter, IndustrialKpisResponse, IndustrialScore } from '@/types/industrial'
import { apiRequest } from './client'

const query = (filter: IndustrialFilter) => ({
  year: filter.year ?? undefined,
  month: filter.month ?? undefined,
  machineId: filter.machineId ?? undefined,
  lineId: filter.lineId ?? undefined,
})

/** Módulo industrial: KPIs de producción y costos y score industrial (todos los roles). */
export const industrialApi = {
  kpis: (filter: IndustrialFilter = {}, signal?: AbortSignal) =>
    apiRequest<IndustrialKpisResponse>('/industrial/kpis', { query: query(filter), signal }),
  score: (filter: IndustrialFilter = {}, signal?: AbortSignal) =>
    apiRequest<IndustrialScore>('/industrial/score', { query: query(filter), signal }),
}
