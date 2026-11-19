import type { HealthResponse } from '@/types/health'
import { API_URL } from './client'

/** GET /health del API (público). El API devuelve 503 con el mismo cuerpo si algún check falla. */
export async function fetchHealth(signal?: AbortSignal): Promise<HealthResponse> {
  const response = await fetch(`${API_URL}/health`, {
    signal,
    headers: { Accept: 'application/json' },
  })
  if (!response.ok && response.status !== 503) {
    throw new Error(`El API respondió ${response.status}`)
  }
  return (await response.json()) as HealthResponse
}
