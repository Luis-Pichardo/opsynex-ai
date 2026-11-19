import { TriangleAlert } from 'lucide-react'
import { Link } from 'react-router'

import { Button } from '@/components/ui/button'
import { formatNumber } from '@/lib/format'

/** Aviso de revisión recomendada (cuentas dudosas). */
export function ReviewNotice({ uploadId, count }: { uploadId: string; count: number }) {
  return (
    <div
      role="status"
      className="flex flex-col gap-3 rounded border border-warning bg-warning-soft p-4 text-warning-text sm:flex-row sm:items-center sm:justify-between"
    >
      <p className="flex items-start gap-2 text-body">
        <TriangleAlert className="mt-1 size-4 shrink-0" aria-hidden="true" />
        <span>
          Algunas cuentas se clasificaron con baja confianza
          {count > 0 ? ` (${formatNumber(count)})` : ''}. Revisa la homologación para mayor
          precisión.
        </span>
      </p>
      <Button asChild variant="outline" className="shrink-0">
        <Link to={`/mappings?uploadId=${uploadId}`}>Revisar</Link>
      </Button>
    </div>
  )
}
