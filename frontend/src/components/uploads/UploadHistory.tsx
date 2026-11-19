import { useQuery } from '@tanstack/react-query'
import type { ColumnDef } from '@tanstack/react-table'
import { FileClock } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link } from 'react-router'

import { uploadsApi } from '@/api/uploads'
import { EmptyState } from '@/components/common/EmptyState'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DataTable } from '@/components/ui/data-table'
import { EMPTY_VALUE, formatDate, formatNumber } from '@/lib/format'
import { FILE_TYPE_LABELS, UPLOAD_STATUS } from '@/lib/labels'
import { useWorkspace } from '@/workspace/useWorkspace'
import type { UploadListItem } from '@/types/uploads'
import { ReportDialog } from './ReportDialog'

/** Historial de cargas de la empresa (más reciente primero). */
export function UploadHistory() {
  const { companyId } = useWorkspace()
  const [selected, setSelected] = useState<string | null>(null)
  const uploads = useQuery({
    queryKey: ['uploads', 'list', companyId],
    queryFn: ({ signal }) => uploadsApi.list(signal),
  })

  const columns = useMemo<ColumnDef<UploadListItem, unknown>[]>(
    () => [
      {
        accessorKey: 'fileName',
        header: 'Archivo',
        cell: ({ row }) => (
          <div className="min-w-48">
            <p className="truncate font-medium text-foreground">{row.original.fileName}</p>
            <p className="text-small text-foreground-secondary">
              {FILE_TYPE_LABELS[row.original.fileType]}
            </p>
          </div>
        ),
      },
      {
        accessorKey: 'status',
        header: 'Estado',
        cell: ({ row }) => {
          const status = UPLOAD_STATUS[row.original.status]
          return (
            <div className="flex flex-wrap gap-1">
              <Badge variant={status.variant}>{status.label}</Badge>
              {row.original.needsReview ? (
                <Badge variant="warning">Revisión recomendada</Badge>
              ) : null}
            </div>
          )
        },
      },
      {
        accessorKey: 'createdAt',
        header: 'Fecha',
        cell: ({ row }) => (
          <span className="whitespace-nowrap tabular-nums">
            {formatDate(row.original.createdAt, { withTime: true })}
          </span>
        ),
      },
      {
        accessorKey: 'uploadedBy',
        header: 'Usuario',
        cell: ({ row }) => row.original.uploadedBy ?? EMPTY_VALUE,
      },
      {
        id: 'years',
        header: 'Años',
        cell: ({ row }) => (
          <span className="tabular-nums">{row.original.years.join(', ') || EMPTY_VALUE}</span>
        ),
      },
      {
        accessorKey: 'rowsCount',
        header: 'Filas',
        meta: { numeric: true },
        cell: ({ row }) => formatNumber(row.original.rowsCount),
      },
      {
        accessorKey: 'warningsCount',
        header: 'Advertencias',
        meta: { numeric: true },
        cell: ({ row }) => formatNumber(row.original.warningsCount),
      },
      {
        id: 'actions',
        header: () => <span className="sr-only">Acciones</span>,
        meta: { actions: true },
        enableSorting: false,
        cell: ({ row }) => (
          <div className="flex justify-end gap-1">
            {row.original.needsReview ? (
              <Button asChild variant="ghost" size="sm">
                <Link to={`/mappings?uploadId=${row.original.id}`}>Revisar</Link>
              </Button>
            ) : null}
            <Button variant="ghost" size="sm" onClick={() => setSelected(row.original.id)}>
              Ver reporte
            </Button>
          </div>
        ),
      },
    ],
    [],
  )

  return (
    <>
      <DataTable
        columns={columns}
        data={uploads.data ?? []}
        loading={uploads.isLoading}
        getRowId={(u) => u.id}
        empty={
          <EmptyState
            icon={FileClock}
            title="Todavía no hay cargas"
            description="Cuando subas el primer archivo de la empresa aparecerá aquí con su estado."
          />
        }
      />
      <ReportDialog uploadId={selected} onOpenChange={(open) => !open && setSelected(null)} />
    </>
  )
}
