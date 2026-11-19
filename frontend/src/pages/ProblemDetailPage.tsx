import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Calculator, Lightbulb, SearchX, Sparkles } from 'lucide-react'
import { Link, useParams } from 'react-router'
import { toast } from 'sonner'

import { kpisApi } from '@/api/kpis'
import { problemsApi } from '@/api/results'
import { ROLES, hasRole } from '@/auth/permissions'
import { useAuth } from '@/auth/useAuth'
import { ChartCard } from '@/components/charts/ChartCard'
import { PRIMARY_SERIES } from '@/components/charts/chart-theme'
import { TrendLineChart } from '@/components/charts/TrendLineChart'
import { DownloadReportButton } from '@/components/common/DownloadReportButton'
import { EmptyState } from '@/components/common/EmptyState'
import { SeverityBadge } from '@/components/common/SeverityBadge'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { AiRecommendationPanel } from '@/components/problems/AiRecommendationPanel'
import { evidenceRows } from '@/lib/evidence'
import { formatCurrency, formatDate, formatNumber, formatPp } from '@/lib/format'
import { formatKpi } from '@/lib/kpi-format'
import { IMPACT_CATEGORY_LABELS, PROBLEM_CATEGORY_LABELS, PROBLEM_STATUS } from '@/lib/labels'
import { useWorkspace } from '@/workspace/useWorkspace'
import type { ProblemStatus } from '@/types/analysis'

/** Detalle de problema (sección 14): descripción, severidad, impacto y su cálculo, histórico, evidencia y recomendaciones. */
export function ProblemDetailPage() {
  const { id = '' } = useParams()
  const { user } = useAuth()
  const { currency, companyId } = useWorkspace()
  const queryClient = useQueryClient()
  const problem = useQuery({
    queryKey: ['problems', 'detail', id],
    queryFn: ({ signal }) => problemsApi.get(id, signal),
  })
  const kpis = useQuery({
    queryKey: ['kpis', 'all', companyId],
    queryFn: ({ signal }) => kpisApi.get(null, signal),
    enabled: companyId !== null,
  })

  const status = useMutation({
    mutationFn: (next: ProblemStatus) => problemsApi.setStatus(id, next),
    onSuccess: (updated) => {
      queryClient.setQueryData(['problems', 'detail', id], updated)
      void queryClient.invalidateQueries({ queryKey: ['problems', 'list'] })
      toast.success(`Estado cambiado a «${PROBLEM_STATUS[updated.status].label}».`)
    },
  })

  if (problem.isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-24" />
        <div className="grid gap-4 lg:grid-cols-3">
          <Skeleton className="h-64 lg:col-span-2" />
          <Skeleton className="h-64" />
        </div>
      </div>
    )
  }

  if (!problem.data) {
    return (
      <EmptyState
        icon={SearchX}
        title="No encontramos este problema"
        description="Puede que ya no se detecte después del último análisis o que sea de otra empresa."
        action={
          <Button asChild variant="outline">
            <Link to="/problems">Volver a Problemas</Link>
          </Button>
        }
      />
    )
  }

  const p = problem.data
  const impact = p.evidence.impact
  const definition = kpis.data?.definitions.find((d) => d.code === p.relatedKpi)
  const unit = definition?.unit ?? 'Ratio'
  const history = p.kpiHistory.map((h) => ({ year: String(h.year), value: h.value }))
  const formatValue = (v: number) => formatKpi(v, unit, currency, true)
  const canEdit = hasRole(user, ROLES.analyze)
  const statusInfo = PROBLEM_STATUS[p.status]

  return (
    <div className="space-y-6">
      <Button asChild variant="link" className="h-auto px-0">
        <Link to="/problems">
          <ArrowLeft />
          Problemas
        </Link>
      </Button>

      <header className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div className="space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <SeverityBadge severity={p.severity} />
            <Badge variant={statusInfo.variant}>{statusInfo.label}</Badge>
            <Badge variant="outline">{PROBLEM_CATEGORY_LABELS[p.category]}</Badge>
            <span className="text-small tabular-nums text-foreground-secondary">Año {p.year}</span>
          </div>
          <h1 className="text-h1 text-foreground">{p.title}</h1>
          <p className="max-w-prose text-body text-foreground-secondary">{p.description}</p>
        </div>
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end lg:flex-col lg:items-stretch">
          {canEdit ? (
            <div className="space-y-1.5 sm:w-56">
              <Label htmlFor="problem-status">Estado</Label>
              <Select
                value={p.status}
                onValueChange={(v) => status.mutate(v as ProblemStatus)}
                disabled={status.isPending}
              >
                <SelectTrigger id="problem-status">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(Object.keys(PROBLEM_STATUS) as ProblemStatus[]).map((s) => (
                    <SelectItem key={s} value={s}>
                      {PROBLEM_STATUS[s].label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          ) : null}
          <DownloadReportButton year={p.year} />
        </div>
      </header>

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="space-y-4 p-4 sm:p-5 lg:col-span-1">
          <div className="space-y-0.5">
            <h2 className="flex items-center gap-2 text-h3 text-foreground">
              <Calculator className="size-4 text-foreground-secondary" aria-hidden="true" />
              Impacto económico
            </h2>
            <p className="text-small text-foreground-secondary">
              {IMPACT_CATEGORY_LABELS[p.impactCategory]}
            </p>
          </div>
          {p.impactValue != null ? (
            <p className="text-kpi tabular-nums text-foreground">
              {formatCurrency(p.impactValue, currency)}
            </p>
          ) : (
            <p className="text-body text-foreground-secondary">
              {impact?.reason ?? 'No se cuantifica como pérdida.'}
            </p>
          )}
          {impact ? (
            <dl className="grid grid-cols-2 gap-3 text-small">
              <div>
                <dt className="text-foreground-secondary">Margen afectado</dt>
                <dd className="font-medium tabular-nums text-foreground">
                  {impact.affectedMarginPp == null
                    ? '—'
                    : formatPp(impact.affectedMarginPp).replace('+', '')}
                </dd>
              </div>
              <div>
                <dt className="text-foreground-secondary">Deterioro</dt>
                <dd className="font-medium tabular-nums text-foreground">
                  {impact.deteriorationMagnitude == null
                    ? '—'
                    : `${formatNumber(impact.deteriorationMagnitude, 1)} %`}
                </dd>
              </div>
            </dl>
          ) : null}
          {impact && impact.formula !== '—' ? (
            <div className="space-y-2 rounded bg-surface-muted p-3">
              <p className="text-small font-medium text-foreground">Cómo se calculó</p>
              <p className="text-small text-foreground-secondary">{impact.formula}</p>
              <dl className="space-y-1">
                {evidenceRows(impact.inputs, currency, unit).map((row) => (
                  <div key={row.label} className="flex justify-between gap-3 text-small">
                    <dt className="text-foreground-secondary">{row.label}</dt>
                    <dd className="tabular-nums text-foreground">{row.value}</dd>
                  </div>
                ))}
              </dl>
            </div>
          ) : null}
        </Card>

        <div className="lg:col-span-2">
          {p.relatedKpi && history.length > 0 ? (
            <ChartCard
              className="h-full"
              title={`Histórico: ${definition?.name ?? p.relatedKpi}`}
              subtitle={`Valor por año, ${history[0]?.year}–${history.at(-1)?.year}`}
              ariaLabel={history
                .map((h) => `${h.year}: ${h.value == null ? 'sin dato' : formatValue(h.value)}`)
                .join(', ')}
              table={{
                columns: [{ label: 'Año' }, { label: definition?.name ?? 'Valor', numeric: true }],
                rows: history.map((h) => [h.year, h.value == null ? '—' : formatValue(h.value)]),
              }}
            >
              <TrendLineChart
                data={history}
                xKey="year"
                series={[
                  { key: 'value', name: definition?.name ?? 'Valor', color: PRIMARY_SERIES },
                ]}
                formatValue={formatValue}
              />
            </ChartCard>
          ) : (
            <Card className="h-full p-4 sm:p-5">
              <h2 className="text-h3 text-foreground">Histórico</h2>
              <p className="text-small text-foreground-secondary">
                Este problema no tiene un KPI asociado para graficar.
              </p>
            </Card>
          )}
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="space-y-3 p-4 sm:p-5 lg:col-span-2">
          <h2 className="flex items-center gap-2 text-h3 text-foreground">
            <Lightbulb className="size-4 text-primary-text" aria-hidden="true" />
            Recomendación
          </h2>
          <Tabs defaultValue={p.aiRecommendation ? 'ia' : 'base'}>
            <TabsList>
              <TabsTrigger value="base">Recomendación base</TabsTrigger>
              <TabsTrigger value="ia">
                <Sparkles aria-hidden="true" />
                Recomendación IA
              </TabsTrigger>
            </TabsList>
            <TabsContent value="base" className="mt-4">
              <p className="max-w-prose text-body text-foreground">{p.recommendation}</p>
            </TabsContent>
            <TabsContent value="ia" className="mt-4">
              <AiRecommendationPanel recommendation={p.aiRecommendation} severity={p.severity} />
            </TabsContent>
          </Tabs>
        </Card>
        <Card className="space-y-3 p-4 sm:p-5">
          <h2 className="text-h3 text-foreground">Evidencia</h2>
          <dl className="space-y-1.5">
            {evidenceRows(p.evidence, currency, unit).map((row) => (
              <div key={row.label} className="flex justify-between gap-3 text-small">
                <dt className="text-foreground-secondary">{row.label}</dt>
                <dd className="tabular-nums text-foreground">{row.value}</dd>
              </div>
            ))}
          </dl>
          <p className="text-caption text-foreground-muted">
            Actualizado el {formatDate(p.updatedAt, { withTime: true })}
          </p>
        </Card>
      </div>
    </div>
  )
}
