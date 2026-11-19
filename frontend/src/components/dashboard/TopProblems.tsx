import { ArrowRight, CircleCheck, Lightbulb, Sparkles } from 'lucide-react'
import { Link } from 'react-router'

import { EmptyState } from '@/components/common/EmptyState'
import { SeverityBadge } from '@/components/common/SeverityBadge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { formatCurrency, formatNumber } from '@/lib/format'
import type { Highlight, ProblemListItem } from '@/types/analysis'

interface TopProblemsProps {
  problems: ProblemListItem[]
  total: number
  currency: string
  loading?: boolean
}

/** Top 5 problemas por prioridad (severidad y luego impacto). */
export function TopProblems({ problems, total, currency, loading }: TopProblemsProps) {
  return (
    <Card className="flex h-full flex-col gap-4 p-4 sm:p-5">
      <div className="flex items-start justify-between gap-3">
        <div className="space-y-0.5">
          <h2 className="text-h3 text-foreground">Principales problemas</h2>
          <p className="text-small text-foreground-secondary">Por severidad e impacto económico</p>
        </div>
        {total > problems.length ? (
          <Button asChild variant="link" size="sm" className="px-0">
            <Link to="/problems">
              Ver los {formatNumber(total)}
              <ArrowRight />
            </Link>
          </Button>
        ) : null}
      </div>
      {loading ? (
        <div className="space-y-3">
          {Array.from({ length: 3 }, (_, i) => (
            <Skeleton key={i} className="h-14" />
          ))}
        </div>
      ) : problems.length === 0 ? (
        <EmptyState
          icon={CircleCheck}
          title="No se detectaron problemas"
          description="Los indicadores de este año están dentro de los umbrales."
          className="py-6"
        />
      ) : (
        <ol className="divide-y divide-border">
          {problems.map((problem) => (
            <li key={problem.id}>
              <Link
                to={`/problems/${problem.id}`}
                className="flex flex-wrap items-center gap-x-3 gap-y-1 py-3 transition-colors duration-150 ease-out hover:bg-surface-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring sm:flex-nowrap"
              >
                <span className="w-5 text-small tabular-nums text-foreground-muted">
                  {problem.priority}
                </span>
                <SeverityBadge severity={problem.severity} />
                <span className="min-w-0 flex-1 basis-40 truncate text-body font-medium text-foreground">
                  {problem.title}
                </span>
                <span className="ml-auto whitespace-nowrap text-body font-semibold tabular-nums text-foreground">
                  {problem.impactValue == null
                    ? 'Sin cuantificar'
                    : formatCurrency(problem.impactValue, currency, { compact: true })}
                </span>
              </Link>
            </li>
          ))}
        </ol>
      )}
    </Card>
  )
}

/** Recomendaciones de los problemas principales y lo que mejoró. */
export function Recommendations({
  problems,
  highlights,
  loading,
}: {
  problems: ProblemListItem[]
  highlights: Highlight[]
  loading?: boolean
}) {
  return (
    <Card className="flex h-full flex-col gap-4 p-4 sm:p-5">
      <div className="space-y-0.5">
        <h2 className="text-h3 text-foreground">Recomendaciones</h2>
        <p className="text-small text-foreground-secondary">
          Para los problemas de mayor prioridad
        </p>
      </div>
      {loading ? (
        <Skeleton className="h-40" />
      ) : (
        <>
          {problems.length === 0 ? (
            <p className="text-small text-foreground-secondary">
              Sin recomendaciones: no hay problemas este año.
            </p>
          ) : (
            <ul className="space-y-4">
              {problems.slice(0, 3).map((problem) => (
                <li key={problem.id} className="flex gap-3">
                  <Lightbulb
                    className="mt-0.5 size-4 shrink-0 text-primary-text"
                    aria-hidden="true"
                  />
                  <div className="space-y-1">
                    <p className="text-small font-medium text-foreground">{problem.title}</p>
                    <p className="max-w-prose text-small text-foreground-secondary">
                      {problem.recommendation}
                    </p>
                  </div>
                </li>
              ))}
            </ul>
          )}
          {highlights.length > 0 ? (
            <div className="space-y-2 rounded bg-good-soft p-3">
              <p className="flex items-center gap-1.5 text-small font-medium text-good-text">
                <Sparkles className="size-4" aria-hidden="true" />
                Lo que mejoró
              </p>
              <ul className="space-y-1">
                {highlights.slice(0, 4).map((h) => (
                  <li key={h.code} className="text-small text-foreground">
                    <span className="font-medium">{h.title}:</span> {h.detail}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </>
      )}
    </Card>
  )
}
