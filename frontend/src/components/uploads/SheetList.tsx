import { EyeOff } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { formatNumber, formatYears } from '@/lib/format'
import { SHEET_TYPE_LABELS } from '@/lib/labels'
import { cn } from '@/lib/utils'
import type { UploadReport } from '@/types/uploads'

/** Paso 2: hojas detectadas con su tipo, filas extraídas y, si se ignoran, el motivo. */
export function SheetList({ report }: { report: UploadReport }) {
  const used = report.sheets.filter((s) => !s.ignored)
  const ignored = report.sheets.filter((s) => s.ignored)
  return (
    <div className="space-y-4">
      <dl className="grid gap-3 sm:grid-cols-3">
        <div className="rounded border border-border bg-surface p-4">
          <dt className="text-overline uppercase text-foreground-secondary">Años encontrados</dt>
          <dd className="text-h3 tabular-nums text-foreground">{formatYears(report.years)}</dd>
        </div>
        <div className="rounded border border-border bg-surface p-4">
          <dt className="text-overline uppercase text-foreground-secondary">Hojas usadas</dt>
          <dd className="text-h3 tabular-nums text-foreground">
            {formatNumber(used.length)} de {formatNumber(report.sheets.length)}
          </dd>
        </div>
        <div className="rounded border border-border bg-surface p-4">
          <dt className="text-overline uppercase text-foreground-secondary">Filas extraídas</dt>
          <dd className="text-h3 tabular-nums text-foreground">{formatNumber(report.rowsCount)}</dd>
        </div>
      </dl>
      <ul
        className="divide-y divide-border overflow-hidden rounded border border-border bg-surface"
        aria-label="Hojas del archivo"
      >
        {[...used, ...ignored].map((sheet) => (
          <li
            key={sheet.name}
            className={cn(
              'grid grid-cols-[1fr_auto] items-center gap-x-3 gap-y-1 px-4 py-3',
              sheet.ignored && 'opacity-60',
            )}
          >
            <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1">
              <span className="truncate font-mono text-small text-foreground">{sheet.name}</span>
              {sheet.ignored ? (
                <Badge variant="neutral">
                  <EyeOff aria-hidden="true" />
                  Ignorada
                </Badge>
              ) : (
                <Badge variant="info" icon={false}>
                  {SHEET_TYPE_LABELS[sheet.detectedType]}
                </Badge>
              )}
            </div>
            <span className="text-right text-small tabular-nums text-foreground-secondary">
              {sheet.ignored ? '' : `${formatNumber(sheet.rowsExtracted)} filas`}
            </span>
            {sheet.reason ? (
              <p className="col-span-2 text-small text-foreground-secondary">{sheet.reason}</p>
            ) : null}
          </li>
        ))}
      </ul>
    </div>
  )
}
