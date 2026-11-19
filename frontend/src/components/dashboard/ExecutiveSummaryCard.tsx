import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CircleAlert, Info, RefreshCw, Sparkles } from 'lucide-react'
import { useEffect, useRef } from 'react'

import { aiApi } from '@/api/results'
import { ROLES, hasRole } from '@/auth/permissions'
import { useAuth } from '@/auth/useAuth'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { formatDate } from '@/lib/format'
import type { AiSummary } from '@/types/analysis'

const POLL_MS = 3000

/**
 * Resumen ejecutivo generado con IA: etiqueta "Generado con IA", fecha, prioridades y "Regenerar".
 * Mientras se genera consulta el estado cada 3 s; si la IA no está disponible lo explica y la
 * plataforma sigue con las recomendaciones base.
 */
export function ExecutiveSummaryCard({ companyId, year }: { companyId: string; year: number }) {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const canRegenerate = hasRole(user, ROLES.analyze)
  const key = ['ai', 'summary', companyId, year]

  const summary = useQuery({
    queryKey: key,
    queryFn: ({ signal }) => aiApi.summary(year, signal),
    refetchInterval: (query) => (query.state.data?.status === 'pending' ? POLL_MS : false),
    meta: { silentError: true },
  })

  // Al terminar de generarse, se refrescan los problemas (traen la recomendación de IA).
  const previous = useRef<AiSummary['status'] | undefined>(undefined)
  const status = summary.data?.status
  useEffect(() => {
    if (previous.current === 'pending' && status !== 'pending') {
      void queryClient.invalidateQueries({ queryKey: ['problems'] })
    }
    previous.current = status
  }, [status, queryClient])

  const regenerate = useMutation({
    mutationFn: () => aiApi.regenerate(year),
    onSuccess: (data) => queryClient.setQueryData(key, data),
  })

  const data = summary.data
  return (
    <Card
      className="flex flex-col gap-4 p-4 sm:p-5"
      aria-labelledby="ai-summary-title"
      aria-busy={status === 'pending'}
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <h2 id="ai-summary-title" className="text-h3 text-foreground">
              Resumen ejecutivo
            </h2>
            {data?.status === 'ready' ? (
              <Badge variant="info" icon={false}>
                <Sparkles aria-hidden="true" />
                Generado con IA
              </Badge>
            ) : null}
          </div>
          <p className="text-small text-foreground-secondary">
            {data?.generatedAt
              ? `Año ${year} · generado el ${formatDate(data.generatedAt, { withTime: true })}`
              : `Lectura gerencial del año ${year}`}
          </p>
        </div>
        {canRegenerate && data && ['ready', 'none', 'unavailable'].includes(data.status) ? (
          <Button
            variant="outline"
            size="sm"
            onClick={() => regenerate.mutate()}
            loading={regenerate.isPending}
          >
            <RefreshCw />
            {data.status === 'ready'
              ? 'Regenerar'
              : data.status === 'none'
                ? 'Generar'
                : 'Reintentar'}
          </Button>
        ) : null}
      </div>
      <SummaryBody summary={data} loading={summary.isLoading} failed={summary.isError} />
    </Card>
  )
}

function SummaryBody({
  summary,
  loading,
  failed,
}: {
  summary: AiSummary | undefined
  loading: boolean
  failed: boolean
}) {
  if (loading) return <Lines />
  if (failed || !summary) {
    return (
      <Notice
        tone="warning"
        text="No se pudo consultar el resumen con IA. Las recomendaciones base siguen disponibles."
      />
    )
  }

  switch (summary.status) {
    case 'pending':
      return (
        <div className="space-y-3" aria-live="polite">
          <p className="flex items-center gap-2 text-small text-foreground-secondary">
            <Sparkles className="size-4 animate-pulse text-primary-text" aria-hidden="true" />
            Generando el análisis con IA… puede tardar un minuto.
          </p>
          <Lines />
        </div>
      )
    case 'ready':
      return (
        <div className="grid gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
          <div className="max-w-prose space-y-3">
            {summary.paragraphs.map((paragraph, i) => (
              <p key={i} className="text-body text-foreground">
                {paragraph}
              </p>
            ))}
          </div>
          <div className="space-y-2">
            <p className="text-overline uppercase text-foreground-secondary">Prioridades</p>
            <ol className="space-y-2">
              {summary.priorities.map((priority, i) => (
                <li
                  key={i}
                  className="flex gap-3 rounded bg-surface-muted p-3 text-small text-foreground"
                >
                  <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-primary-soft text-caption tabular-nums text-primary-text">
                    {i + 1}
                  </span>
                  {priority}
                </li>
              ))}
            </ol>
            {summary.message ? <Notice tone="warning" text={summary.message} /> : null}
            <p className="text-caption text-foreground-muted">
              Generado con IA a partir de los indicadores de la empresa: es orientativo; verifica
              las cifras antes de decidir.
            </p>
          </div>
        </div>
      )
    case 'unavailable':
      return (
        <Notice
          tone="warning"
          text={`${summary.message ?? 'El servicio de IA no está disponible.'} Las recomendaciones base siguen disponibles.`}
        />
      )
    default:
      return <Notice tone="info" text={summary.message ?? 'Todavía no hay resumen con IA.'} />
  }
}

function Lines() {
  return (
    <div className="space-y-2">
      <Skeleton className="h-4 w-full" />
      <Skeleton className="h-4 w-11/12" />
      <Skeleton className="h-4 w-4/5" />
    </div>
  )
}

function Notice({ tone, text }: { tone: 'info' | 'warning'; text: string }) {
  const Icon = tone === 'info' ? Info : CircleAlert
  return (
    <p
      role={tone === 'warning' ? 'alert' : 'status'}
      className={
        tone === 'info'
          ? 'flex items-start gap-2 text-small text-foreground-secondary'
          : 'flex items-start gap-2 rounded bg-warning-soft p-3 text-small text-warning-text'
      }
    >
      <Icon className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
      {text}
    </p>
  )
}
