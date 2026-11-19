import { useState } from 'react'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { formatNumber } from '@/lib/format'
import { issueLocation } from '@/lib/issues'
import type { UploadIssue } from '@/types/uploads'

const VISIBLE = 5

interface IssueListProps {
  kind: 'error' | 'warning'
  issues: UploadIssue[]
}

/** Errores bloqueantes o advertencias de calidad con hoja, fila y columna. */
export function IssueList({ kind, issues }: IssueListProps) {
  const [expanded, setExpanded] = useState(false)
  if (issues.length === 0) return null
  const shown = expanded ? issues : issues.slice(0, VISIBLE)
  const isError = kind === 'error'
  return (
    <section className="space-y-3" aria-label={isError ? 'Errores' : 'Advertencias'}>
      <h3 className="flex items-center gap-2 text-h3 text-foreground">
        {isError ? 'Errores que impiden continuar' : 'Advertencias de calidad'}
        <Badge variant={isError ? 'critical' : 'warning'}>{formatNumber(issues.length)}</Badge>
      </h3>
      {!isError ? (
        <p className="text-small text-foreground-secondary">
          No bloquean el análisis, pero conviene revisarlas con el contador.
        </p>
      ) : null}
      <ul className="space-y-2">
        {shown.map((issue, index) => {
          const location = issueLocation(issue)
          return (
            <li
              key={`${issue.code}-${index}`}
              className={
                isError
                  ? 'rounded border border-critical bg-critical-soft p-3 text-critical-text'
                  : 'rounded border border-border bg-surface p-3'
              }
            >
              <p className={isError ? 'text-body' : 'text-body text-foreground'}>{issue.message}</p>
              {location ? (
                <p className={isError ? 'text-small' : 'text-small text-foreground-secondary'}>
                  {location}
                </p>
              ) : null}
            </li>
          )
        })}
      </ul>
      {issues.length > VISIBLE ? (
        <Button variant="link" className="h-auto px-0" onClick={() => setExpanded((v) => !v)}>
          {expanded ? 'Ver menos' : `Ver las ${formatNumber(issues.length)}`}
        </Button>
      ) : null}
    </section>
  )
}
