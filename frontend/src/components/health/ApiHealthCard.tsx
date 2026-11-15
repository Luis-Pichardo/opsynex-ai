import { useQuery } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'

import { ApiStatusBadge, type ApiConnectionState } from '@/components/health/ApiStatusBadge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { API_URL, fetchHealth } from '@/lib/api'
import { formatDate, formatNumber } from '@/lib/format'

/** Tarjeta que consulta GET /health del API y muestra su estado. */
export function ApiHealthCard() {
  const health = useQuery({
    queryKey: ['api-health'],
    queryFn: ({ signal }) => fetchHealth(signal),
    refetchInterval: 30_000,
    retry: false,
  })

  const state: ApiConnectionState = health.isPending
    ? 'loading'
    : health.isError
      ? 'error'
      : health.data.status

  return (
    <Card>
      <CardHeader>
        <div className="space-y-1">
          <CardTitle>Estado del API</CardTitle>
          <CardDescription className="break-all">{API_URL}</CardDescription>
        </div>
        <ApiStatusBadge state={state} />
      </CardHeader>
      <CardContent className="space-y-4">
        {health.isError ? (
          <p className="text-small text-critical-text">
            No se pudo conectar con el API. Verifica que esté en ejecución y que{' '}
            <code className="font-mono">VITE_API_URL</code> apunte a él.
          </p>
        ) : (
          <dl className="grid grid-cols-2 gap-4 text-small">
            <div>
              <dt className="text-overline uppercase text-foreground-muted">Versión</dt>
              <dd className="tabular-nums text-foreground">{health.data?.version ?? '—'}</dd>
            </div>
            <div>
              <dt className="text-overline uppercase text-foreground-muted">Respuesta</dt>
              <dd className="tabular-nums text-foreground">
                {health.data ? `${formatNumber(health.data.totalDurationMs, 1)} ms` : '—'}
              </dd>
            </div>
          </dl>
        )}
        <div className="flex flex-wrap items-center justify-between gap-2">
          <span className="text-caption text-foreground-muted">
            Última comprobación:{' '}
            {health.dataUpdatedAt || health.errorUpdatedAt
              ? formatDate(new Date(Math.max(health.dataUpdatedAt, health.errorUpdatedAt)), {
                  withTime: true,
                })
              : '—'}
          </span>
          <Button
            variant="outline"
            size="sm"
            onClick={() => void health.refetch()}
            disabled={health.isFetching}
          >
            <RefreshCw
              aria-hidden="true"
              className={health.isFetching ? 'animate-spin' : undefined}
            />
            {health.isFetching ? 'Comprobando…' : 'Comprobar de nuevo'}
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}
