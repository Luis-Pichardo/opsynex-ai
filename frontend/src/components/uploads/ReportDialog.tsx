import { useQuery } from '@tanstack/react-query'

import { uploadsApi } from '@/api/uploads'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Skeleton } from '@/components/ui/skeleton'
import { formatDate } from '@/lib/format'
import { IssueList } from './IssueList'
import { SheetList } from './SheetList'
import { ReviewNotice } from './ReviewNotice'

interface ReportDialogProps {
  uploadId: string | null
  onOpenChange: (open: boolean) => void
}

/** Reporte de una carga del historial: hojas, errores, advertencias y revisión recomendada. */
export function ReportDialog({ uploadId, onOpenChange }: ReportDialogProps) {
  const report = useQuery({
    queryKey: ['uploads', uploadId],
    queryFn: ({ signal }) => uploadsApi.get(uploadId!, signal),
    enabled: uploadId !== null,
  })
  const data = report.data

  return (
    <Dialog open={uploadId !== null} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle className="break-all">{data?.fileName ?? 'Reporte de la carga'}</DialogTitle>
          <DialogDescription>
            {data ? `Subido el ${formatDate(data.createdAt, { withTime: true })}` : 'Cargando…'}
          </DialogDescription>
        </DialogHeader>
        {!data ? (
          <div className="space-y-3">
            <Skeleton className="h-16" />
            <Skeleton className="h-40" />
          </div>
        ) : (
          <div className="space-y-6">
            {data.needsReview ? (
              <ReviewNotice uploadId={data.uploadId} count={data.doubtfulAccounts} />
            ) : null}
            <IssueList kind="error" issues={data.errors} />
            <SheetList report={data} />
            <IssueList kind="warning" issues={data.warnings} />
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}
