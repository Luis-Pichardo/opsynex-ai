import type { CategoryOption, PendingAccount, ProcessingResult } from '@/types/uploads'
import { apiRequest } from './client'

export interface MappingChange {
  id: string
  category: string
}

export const mappingsApi = {
  categories: (signal?: AbortSignal) =>
    apiRequest<CategoryOption[]>('/mappings/categories', { signal }),
  pending: (uploadId: string, signal?: AbortSignal) =>
    apiRequest<PendingAccount[]>('/mappings/pending', { query: { uploadId }, signal }),
  /** Guarda y confirma las categorías elegidas (quedan como mapeos de la empresa). */
  save: (items: MappingChange[]) =>
    apiRequest<unknown[]>('/mappings', { method: 'PUT', body: { items } }),
  /** Vuelve a analizar la carga con los mapeos actuales. */
  consolidate: (uploadId: string) =>
    apiRequest<ProcessingResult>('/mappings/consolidate', { method: 'POST', query: { uploadId } }),
}
