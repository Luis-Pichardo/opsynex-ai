import { Sparkles } from 'lucide-react'

import { EmptyState } from '@/components/common/EmptyState'
import { Badge } from '@/components/ui/badge'
import { formatDate } from '@/lib/format'
import { EFFORT_LABELS, HORIZON_LABELS } from '@/lib/labels'
import type { AiRecommendation, Severity } from '@/types/analysis'

/** Recomendación de IA: diagnóstico, causas raíz y acciones con esfuerzo y horizonte. */
export function AiRecommendationPanel({
  recommendation,
  severity,
}: {
  recommendation: AiRecommendation | null
  severity: Severity
}) {
  if (!recommendation) {
    return (
      <EmptyState
        icon={Sparkles}
        className="py-8"
        title="Sin recomendación de IA"
        description={
          severity === 'Low'
            ? 'La IA genera recomendaciones para los problemas de severidad alta y media.'
            : 'Todavía no se generó o la IA no está disponible. El analista puede pedirla con «Regenerar» en el resumen ejecutivo del dashboard.'
        }
      />
    )
  }

  return (
    <div className="space-y-5">
      <p className="flex flex-wrap items-center gap-2 text-caption text-foreground-muted">
        <Badge variant="info" icon={false}>
          <Sparkles aria-hidden="true" />
          Generado con IA
        </Badge>
        {formatDate(recommendation.generatedAt, { withTime: true })} · {recommendation.model}
      </p>
      <p className="max-w-prose text-body text-foreground">{recommendation.summary}</p>

      <section className="space-y-2" aria-labelledby="ai-causes">
        <h3 id="ai-causes" className="text-overline uppercase text-foreground-secondary">
          Causas raíz probables
        </h3>
        <ol className="list-decimal space-y-1 pl-5 text-body text-foreground">
          {recommendation.rootCauses.map((cause, i) => (
            <li key={i}>{cause}</li>
          ))}
        </ol>
      </section>

      <section className="space-y-2" aria-labelledby="ai-actions">
        <h3 id="ai-actions" className="text-overline uppercase text-foreground-secondary">
          Acciones recomendadas
        </h3>
        <ol className="space-y-3">
          {recommendation.actions.map((action, i) => (
            <li key={i} className="space-y-1.5 rounded border border-border p-3">
              <p className="text-body font-medium text-foreground">
                {i + 1}. {action.title}
              </p>
              <p className="text-small text-foreground-secondary">{action.description}</p>
              <p className="text-small text-foreground">
                <span className="font-medium">Impacto esperado:</span> {action.expectedImpact}
              </p>
              <div className="flex flex-wrap gap-2">
                <Badge variant="neutral">{EFFORT_LABELS[action.effort]}</Badge>
                <Badge variant="outline">{HORIZON_LABELS[action.horizon]}</Badge>
              </div>
            </li>
          ))}
        </ol>
      </section>

      <p className="text-caption text-foreground-muted">
        Las recomendaciones de IA son orientativas y se basan solo en los indicadores de la empresa;
        valídalas con tu equipo antes de actuar.
      </p>
    </div>
  )
}
