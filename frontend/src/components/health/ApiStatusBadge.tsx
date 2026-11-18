import { CircleCheck, LoaderCircle, OctagonAlert, TriangleAlert } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import type { HealthStatus } from '@/lib/api'

export type ApiConnectionState = 'loading' | 'error' | HealthStatus

const STATES = {
  loading: { variant: 'neutral', label: 'Comprobando…', Icon: LoaderCircle },
  Healthy: { variant: 'good', label: 'En línea', Icon: CircleCheck },
  Degraded: { variant: 'serious', label: 'Degradado', Icon: TriangleAlert },
  Unhealthy: { variant: 'critical', label: 'Con fallas', Icon: OctagonAlert },
  error: { variant: 'critical', label: 'Sin conexión', Icon: OctagonAlert },
} as const

/** Estado del API con color semántico + icono + texto (nunca el color solo). */
export function ApiStatusBadge({ state }: { state: ApiConnectionState }) {
  const { variant, label, Icon } = STATES[state]
  return (
    <Badge variant={variant} role="status" aria-live="polite">
      <Icon aria-hidden="true" className={state === 'loading' ? 'animate-spin' : undefined} />
      {label}
    </Badge>
  )
}
