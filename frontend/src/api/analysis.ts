import type { AnalysisSummary, DataResponse } from '@/types/uploads'
import { apiRequest } from './client'

export const analysisApi = {
  summary: (signal?: AbortSignal) => apiRequest<AnalysisSummary>('/analysis/summary', { signal }),
}

export const dataApi = {
  get: (type: 'Financial' | 'Operational', year: number | null, signal?: AbortSignal) =>
    apiRequest<DataResponse>('/data', { query: { type, year }, signal }),
}
