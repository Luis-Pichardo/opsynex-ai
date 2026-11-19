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
