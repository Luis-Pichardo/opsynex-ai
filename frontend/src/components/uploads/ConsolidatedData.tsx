import { useQuery } from '@tanstack/react-query'
import { Database } from 'lucide-react'

import { dataApi } from '@/api/analysis'
import { EmptyState } from '@/components/common/EmptyState'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { formatCurrency, formatNumber } from '@/lib/format'
import { FINANCIAL_GROUPS } from '@/lib/labels'
import { useWorkspace } from '@/workspace/useWorkspace'

const OPERATIONAL_FIELDS: [string, string, 'money' | 'number'][] = [
  ['employeeCount', 'Empleados', 'number'],
  ['unitsProduced', 'Unidades producidas', 'number'],
  ['energyCost', 'Costo de energía', 'money'],
  ['energyConsumptionKwh', 'Consumo de energía (kWh)', 'number'],
  ['operationalCosts', 'Costos operacionales', 'money'],
  ['scrapEstimate', 'Scrap estimado', 'number'],
]

/** Datos consolidados por año (solo lectura): financieros y operacionales. */
export function ConsolidatedData() {
  const { year, currency, companyId } = useWorkspace()
  const financial = useQuery({
    queryKey: ['data', 'Financial', companyId, year],
    queryFn: ({ signal }) => dataApi.get('Financial', year, signal),
    enabled: year !== null,
  })
  const operational = useQuery({
    queryKey: ['data', 'Operational', companyId, year],
    queryFn: ({ signal }) => dataApi.get('Operational', year, signal),
    enabled: year !== null,
  })

  if (year === null) {
    return (
      <Card>
        <EmptyState
          icon={Database}
          title="Todavía no hay datos consolidados"
          description="Sube el libro contable de la empresa para ver aquí sus cifras por año."
        />
      </Card>
    )
  }

  if (financial.isLoading || operational.isLoading) {
    return (
      <div className="grid gap-4 lg:grid-cols-2">
        <Skeleton className="h-80" />
        <Skeleton className="h-80" />
      </div>
    )
  }

  const record = financial.data?.financial?.find((f) => f.year === year)
  const ops = operational.data?.operational?.find((o) => o.year === year)

  return (
    <div className="space-y-4">
      <p className="text-small text-foreground-secondary">
        Cifras de {year} después de clasificar las cuentas (moneda {currency}). Cambia el año en la
        barra superior.
      </p>
      <div className="grid gap-4 lg:grid-cols-2">
        {FINANCIAL_GROUPS.map((group) => (
          <Card key={group.title} className="overflow-hidden">
            <h3 className="px-4 pt-4 text-h3 text-foreground sm:px-5 sm:pt-5">{group.title}</h3>
            <Table className="mt-3">
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead>Concepto</TableHead>
                  <TableHead className="numeric">{year}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {group.fields.map(([key, label]) => {
                  const value = record?.[key]
                  return (
                    <TableRow key={key}>
                      <TableCell>{label}</TableCell>
                      <TableCell className="numeric">
                        {formatCurrency(typeof value === 'number' ? value : null, currency)}
                      </TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          </Card>
        ))}
        <Card className="overflow-hidden">
          <h3 className="px-4 pt-4 text-h3 text-foreground sm:px-5 sm:pt-5">Datos operacionales</h3>
          <Table className="mt-3">
            <TableHeader>
              <TableRow className="hover:bg-transparent">
                <TableHead>Concepto</TableHead>
                <TableHead className="numeric">{year}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {OPERATIONAL_FIELDS.map(([key, label, kind]) => {
                const value = ops?.[key as keyof typeof ops]
                const number = typeof value === 'number' ? value : null
                return (
                  <TableRow key={key}>
                    <TableCell>{label}</TableCell>
                    <TableCell className="numeric">
                      {kind === 'money' ? formatCurrency(number, currency) : formatNumber(number)}
                    </TableCell>
                  </TableRow>
                )
              })}
            </TableBody>
          </Table>
        </Card>
      </div>
    </div>
  )
}
