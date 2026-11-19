import { Table2 } from 'lucide-react'
import { useId, useState, type ReactNode } from 'react'

import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { cn } from '@/lib/utils'

export interface ChartTableData {
  columns: { label: string; numeric?: boolean }[]
  rows: (string | number)[][]
}

interface ChartCardProps {
  title: string
  /** Unidad y período ("Montos anuales en DOP, 2022–2024"). */
  subtitle: string
  /** Descripción del gráfico para lectores de pantalla. */
  ariaLabel: string
  /** Mismos datos en tabla ("Ver tabla"). */
  table: ChartTableData
  actions?: ReactNode
  children: ReactNode
  className?: string
}

/**
 * Tarjeta de gráfico (GUIA_DE_DISENO.md §7): título `h3`, subtítulo con unidad y período,
 * `aria-label` y enlace "Ver tabla" con los mismos datos.
 */
export function ChartCard({
  title,
  subtitle,
  ariaLabel,
  table,
  actions,
  children,
  className,
}: ChartCardProps) {
  const [showTable, setShowTable] = useState(false)
  const tableId = useId()
  return (
    <Card className={cn('flex flex-col gap-4 p-4 sm:p-5', className)}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 space-y-0.5">
          <h3 className="text-h3 text-foreground">{title}</h3>
          <p className="text-small text-foreground-secondary">{subtitle}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {actions}
          <Button
            variant="link"
            size="sm"
            className="px-0"
            aria-expanded={showTable}
            aria-controls={tableId}
            onClick={() => setShowTable((v) => !v)}
          >
            <Table2 aria-hidden="true" />
            {showTable ? 'Ver gráfico' : 'Ver tabla'}
          </Button>
        </div>
      </div>
      {showTable ? (
        <div id={tableId} className="overflow-hidden rounded border border-border">
          <Table>
            <TableHeader>
              <TableRow className="hover:bg-transparent">
                {table.columns.map((column) => (
                  <TableHead key={column.label} className={column.numeric ? 'numeric' : undefined}>
                    {column.label}
                  </TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {table.rows.map((row, i) => (
                <TableRow key={i}>
                  {row.map((cell, j) => (
                    <TableCell
                      key={j}
                      className={table.columns[j]?.numeric ? 'numeric' : undefined}
                    >
                      {cell}
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      ) : (
        <div role="img" aria-label={ariaLabel} id={tableId} className="w-full">
          {children}
        </div>
      )}
    </Card>
  )
}
