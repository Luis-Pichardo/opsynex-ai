import { Factory } from 'lucide-react'

import { Card } from '@/components/ui/card'
import { formatDay, formatNumber } from '@/lib/format'
import { FILE_TYPE_LABELS } from '@/lib/labels'
import type { UploadReport } from '@/types/uploads'

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="space-y-1">
      <p className="text-overline uppercase text-foreground-secondary">{label}</p>
      <p className="text-h3 tabular-nums text-foreground">{value}</p>
    </div>
  )
}

/** Resultado de una carga del módulo industrial: registros, máquinas y período. */
export function IndustrialUploadSummary({ report }: { report: UploadReport }) {
  const { summary } = report
  const period =
    summary.periodFrom && summary.periodTo
      ? `${formatDay(summary.periodFrom)} – ${formatDay(summary.periodTo)}`
      : 'Sin fechas (estándares)'
  return (
    <Card className="space-y-5 p-4 sm:p-5">
      <div className="flex items-start gap-3">
        <span className="flex size-10 shrink-0 items-center justify-center rounded bg-info-soft text-info-text">
          <Factory className="size-5" aria-hidden="true" />
        </span>
        <div className="space-y-1">
          <h3 className="text-h3 text-foreground">{FILE_TYPE_LABELS[report.fileType]}</h3>
          <p className="text-small text-foreground-secondary">
            Se recalcularon los KPIs industriales (OEE, scrap, paradas y costo por unidad), los
            problemas con su impacto y el score industrial.
          </p>
        </div>
      </div>
      <div className="grid gap-4 sm:grid-cols-3">
        <Metric
          label="Registros guardados"
          value={formatNumber(summary.industrialRecords ?? report.rowsCount)}
        />
        <Metric label="Máquinas" value={formatNumber(summary.machines ?? 0)} />
        <Metric label="Período" value={period} />
      </div>
    </Card>
  )
}
