import type { ColumnDef } from '@tanstack/react-table'
import { CircleCheck, ListChecks, SearchX } from 'lucide-react'
import { useMemo } from 'react'

import { EmptyState } from '@/components/common/EmptyState'
import { Badge } from '@/components/ui/badge'
import { DataTable } from '@/components/ui/data-table'
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { SimpleTooltip } from '@/components/ui/tooltip'
import { CONFIDENCE, confidenceLevel } from '@/lib/confidence'
import { formatCurrency, formatNumber, formatPercent } from '@/lib/format'
import { METHOD_LABELS } from '@/lib/labels'
import type { CategoryOption, FinancialStatement, PendingAccount } from '@/types/uploads'

const STATEMENT_LABELS: Record<FinancialStatement, string> = {
  IncomeStatement: 'Estado de resultados',
  BalanceSheet: 'Balance general',
  CashFlow: 'Flujo de efectivo',
  None: 'Otras',
}

export interface MappingReviewTableProps {
  accounts: PendingAccount[]
  categories: CategoryOption[]
  currency: string
  /** Categoría elegida por cuenta (cambios sin guardar), por `mappingId`. */
  edits: Record<string, string>
  onChange: (mappingId: string, category: string) => void
  disabled?: boolean
  /** Texto del estado vacío según el filtro. */
  emptyTitle?: string
}

/**
 * Tabla de homologación: cuenta del cliente (hoja, fila y sección) → categoría (editable),
 * confianza (Alta/Media/Baja con icono) y método.
 */
export function MappingReviewTable({
  accounts,
  categories,
  currency,
  edits,
  onChange,
  disabled,
  emptyTitle = 'No hay cuentas para revisar',
}: MappingReviewTableProps) {
  const grouped = useMemo(() => {
    const groups = new Map<FinancialStatement, CategoryOption[]>()
    for (const option of categories) {
      groups.set(option.statement, [...(groups.get(option.statement) ?? []), option])
    }
    return [...groups.entries()]
  }, [categories])

  const columns = useMemo<ColumnDef<PendingAccount, unknown>[]>(
    () => [
      {
        accessorKey: 'originalLabel',
        header: 'Cuenta del cliente',
        cell: ({ row }) => {
          const a = row.original
          return (
            <div className="min-w-52 max-w-sm">
              <p className="font-medium text-foreground">
                {a.originalLabel || a.clientAccount || 'Subtotal sin nombre'}
              </p>
              <p className="text-small text-foreground-secondary">
                {a.sheetName} · fila {formatNumber(a.rowNumber)}
                {a.section ? ` · ${a.section}` : ''}
                {a.scope === 'Notes' ? ' · nota (detalle)' : ''}
              </p>
            </div>
          )
        },
      },
      {
        accessorKey: 'amount',
        header: 'Monto',
        meta: { numeric: true },
        cell: ({ row }) => (
          <span className="whitespace-nowrap">
            {formatCurrency(row.original.amount, currency)}
            <span className="block text-small text-foreground-secondary">{row.original.year}</span>
          </span>
        ),
      },
      {
        id: 'category',
        header: 'Categoría',
        enableSorting: false,
        cell: ({ row }) => {
          const a = row.original
          const value = edits[a.mappingId] ?? a.category
          const changed = edits[a.mappingId] !== undefined
          return (
            <div className="flex min-w-56 items-center gap-2">
              <Select
                value={value}
                onValueChange={(v) => onChange(a.mappingId, v)}
                disabled={disabled}
              >
                <SelectTrigger
                  aria-label={`Categoría de ${a.originalLabel || a.clientAccount || `fila ${a.rowNumber}`}`}
                  className={changed ? 'border-primary' : undefined}
                >
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {grouped.map(([statement, options]) => (
                    <SelectGroup key={statement}>
                      <SelectLabel>{STATEMENT_LABELS[statement]}</SelectLabel>
                      {options.map((option) => (
                        <SelectItem key={option.category} value={option.category}>
                          {option.name}
                        </SelectItem>
                      ))}
                    </SelectGroup>
                  ))}
                </SelectContent>
              </Select>
              {changed ? (
                <SimpleTooltip content="Se confirmará al guardar">
                  <CircleCheck
                    className="size-4 shrink-0 text-primary-text"
                    aria-label="Por confirmar"
                  />
                </SimpleTooltip>
              ) : null}
            </div>
          )
        },
      },
      {
        accessorKey: 'confidence',
        header: 'Confianza',
        cell: ({ row }) => {
          const a = row.original
          if (a.isConfirmed) return <Badge variant="good">Confirmada</Badge>
          const level = CONFIDENCE[confidenceLevel(a.confidence)]
          return (
            <SimpleTooltip content={`Confianza ${formatPercent(a.confidence, 0)}`}>
              <span
                tabIndex={0}
                className="inline-flex rounded-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                <Badge variant={level.variant}>{level.label}</Badge>
              </span>
            </SimpleTooltip>
          )
        },
      },
      {
        accessorKey: 'method',
        header: 'Método',
        cell: ({ row }) => (
          <span className="whitespace-nowrap text-small text-foreground-secondary">
            {METHOD_LABELS[row.original.method]}
          </span>
        ),
      },
    ],
    [currency, disabled, edits, grouped, onChange],
  )

  return (
    <DataTable
      columns={columns}
      data={accounts}
      pageSize={15}
      getRowId={(a) => a.mappingId}
      empty={
        <EmptyState
          icon={accounts.length === 0 && emptyTitle.includes('dudosas') ? ListChecks : SearchX}
          title={emptyTitle}
        />
      }
    />
  )
}
