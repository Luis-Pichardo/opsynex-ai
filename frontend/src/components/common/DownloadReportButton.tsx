import { useMutation } from '@tanstack/react-query'
import { FileDown } from 'lucide-react'
import { toast } from 'sonner'

import { downloadExecutiveReport } from '@/api/results'
import { Button, type ButtonProps } from '@/components/ui/button'

/** "Descargar informe PDF" (todos los roles): informe ejecutivo del año indicado. */
export function DownloadReportButton({
  year,
  variant = 'outline',
}: {
  year: number | null
  variant?: ButtonProps['variant']
}) {
  const download = useMutation({
    mutationFn: () => downloadExecutiveReport(year),
    onSuccess: () => toast.success('Informe descargado.'),
  })
  return (
    <Button
      variant={variant}
      onClick={() => download.mutate()}
      loading={download.isPending}
      disabled={year === null}
    >
      <FileDown />
      Descargar informe PDF
    </Button>
  )
}
