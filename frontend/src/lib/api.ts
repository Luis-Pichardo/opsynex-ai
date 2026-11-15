/** Cliente mínimo del API .NET. El frontend solo habla con el API, nunca con el servicio de IA. */

export const API_URL = (import.meta.env.VITE_API_URL ?? 'http://localhost:5080').replace(/\/$/, '')

export type HealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy'

export interface HealthCheckEntry {
  name: string
  status: HealthStatus
  description?: string | null
  durationMs: number
}

export interface HealthResponse {
  status: HealthStatus
  service: string
  version?: string | null
  totalDurationMs: number
  checks: HealthCheckEntry[]
}

/** GET /health del API. El API devuelve 503 con el mismo cuerpo si algún check falla. */
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
