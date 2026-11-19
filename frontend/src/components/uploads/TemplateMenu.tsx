import { useMutation } from '@tanstack/react-query'
import { Download } from 'lucide-react'

import { downloadTemplate } from '@/api/uploads'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { FILE_TYPE_LABELS, INDUSTRIAL_FILE_TYPES } from '@/lib/labels'
import type { UploadFileType } from '@/types/uploads'

const TEMPLATES: UploadFileType[] = [
  'IncomeStatement',
  'BalanceSheet',
  'CashFlow',
  'CostStructure',
  'Operational',
]

/** Enlace "¿No tienes estados financieros? Descarga la plantilla" con el menú de plantillas. */
export function TemplateMenu({
  label = '¿No tienes estados financieros? Descarga la plantilla',
}: {
  label?: string
}) {
  const download = useMutation({ mutationFn: downloadTemplate })
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="link" className="h-auto whitespace-normal px-0 text-left">
          <Download aria-hidden="true" />
          {label}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start">
        <DropdownMenuLabel>Plantillas (.xlsx)</DropdownMenuLabel>
        {TEMPLATES.map((type) => (
          <DropdownMenuItem key={type} onSelect={() => download.mutate(type)}>
            <Download aria-hidden="true" />
            {FILE_TYPE_LABELS[type]}
          </DropdownMenuItem>
        ))}
        <DropdownMenuSeparator />
        <DropdownMenuLabel>Módulo industrial (.xlsx)</DropdownMenuLabel>
        {INDUSTRIAL_FILE_TYPES.map((type) => (
          <DropdownMenuItem key={type} onSelect={() => download.mutate(type)}>
            <Download aria-hidden="true" />
            {FILE_TYPE_LABELS[type]}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
