import { useQuery } from '@tanstack/react-query'
import type { ColumnDef } from '@tanstack/react-table'
import { CircleCheck, FilterX } from 'lucide-react'
import { useMemo } from 'react'
import { Link, useSearchParams } from 'react-router'

import { problemsApi } from '@/api/results'
import { EmptyState } from '@/components/common/EmptyState'
import { PageHeader } from '@/components/common/PageHeader'
import { RequireCompany } from '@/components/common/RequireCompany'
import { SeverityBadge } from '@/components/common/SeverityBadge'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DataTable } from '@/components/ui/data-table'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { formatCurrency } from '@/lib/format'
import { PROBLEM_CATEGORY_LABELS, PROBLEM_STATUS } from '@/lib/labels'
import { useWorkspace } from '@/workspace/useWorkspace'
import type { ProblemCategory, ProblemListItem, ProblemStatus, Severity } from '@/types/analysis'

const ALL = 'all'
const SEVERITY_ORDER: Record<Severity, number> = { High: 3, Medium: 2, Low: 1 }
const SEVERITY_LABELS: Record<Severity, string> = { High: 'Alta', Medium: 'Media', Low: 'Baja' }

/** Lista de problemas: filtros (año, severidad, categoría, estado), orden por prioridad o impacto y paginación. */
export function ProblemsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Problemas"
        description="Ineficiencias detectadas, ordenadas por severidad e impacto económico."
      />
      <RequireCompany>
        <ProblemsList />
      </RequireCompany>
    </div>
  )
}

function ProblemsList() {
  const { companyId, currency, years, year: workspaceYear } = useWorkspace()
  const [params, setParams] = useSearchParams()
  const yearParam = params.get('year')
  const filters = {
    year:
      yearParam === ALL ? undefined : yearParam ? Number(yearParam) : (workspaceYear ?? undefined),
    severity: (params.get('severity') as Severity | null) ?? undefined,
    category: (params.get('category') as ProblemCategory | null) ?? undefined,
    status: (params.get('status') as ProblemStatus | null) ?? undefined,
  }
  const setFilter = (key: string, value: string) => {
    const next = new URLSearchParams(params)
    if (value === ALL && key !== 'year') next.delete(key)
    else next.set(key, value)
    setParams(next, { replace: true })
  }

  const problems = useQuery({
    queryKey: ['problems', 'list', companyId, filters],
    queryFn: ({ signal }) => problemsApi.list({ ...filters, pageSize: 100 }, signal),
    placeholderData: (previous) => previous,
  })

  const columns = useMemo<ColumnDef<ProblemListItem, unknown>[]>(
    () => [
      {
        accessorKey: 'priority',
        header: '#',
        meta: { numeric: true },
        cell: ({ row }) => row.original.priority,
      },
      {
        accessorKey: 'title',
        header: 'Problema',
        cell: ({ row }) => (
          <div className="min-w-56 max-w-md">
            <Link
              to={`/problems/${row.original.id}`}
              className="font-medium text-primary-text hover:underline"
            >
              {row.original.title}
            </Link>
            <p className="line-clamp-2 text-small text-foreground-secondary">
              {row.original.description}
            </p>
          </div>
        ),
      },
      {
        accessorKey: 'year',
        header: 'Año',
        cell: ({ row }) => <span className="tabular-nums">{row.original.year}</span>,
      },
      {
        accessorKey: 'severity',
        header: 'Severidad',
        sortingFn: (a, b) =>
          SEVERITY_ORDER[a.original.severity] - SEVERITY_ORDER[b.original.severity],
        cell: ({ row }) => <SeverityBadge severity={row.original.severity} />,
      },
      {
        accessorKey: 'category',
        header: 'Categoría',
        cell: ({ row }) => PROBLEM_CATEGORY_LABELS[row.original.category],
      },
      {
        accessorKey: 'impactValue',
        header: 'Impacto estimado',
        meta: { numeric: true },
        sortUndefined: 'last',
        cell: ({ row }) =>
          row.original.impactValue == null ? (
            <span className="text-foreground-secondary">Sin cuantificar</span>
          ) : (
            <span className="font-semibold">
              {formatCurrency(row.original.impactValue, currency)}
            </span>
          ),
      },
      {
        accessorKey: 'status',
        header: 'Estado',
        cell: ({ row }) => {
          const status = PROBLEM_STATUS[row.original.status]
          return <Badge variant={status.variant}>{status.label}</Badge>
        },
      },
    ],
    [currency],
  )

  const hasFilters = filters.severity || filters.category || filters.status
  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <FilterSelect
          id="filter-year"
          label="Año"
          value={yearParam ?? (workspaceYear ? String(workspaceYear) : ALL)}
          onChange={(v) => setFilter('year', v)}
          options={[
            [ALL, 'Todos los años'],
            ...years.map((y) => [String(y), String(y)] as [string, string]),
          ]}
        />
        <FilterSelect
          id="filter-severity"
          label="Severidad"
          value={filters.severity ?? ALL}
          onChange={(v) => setFilter('severity', v)}
          options={[
            [ALL, 'Todas'],
            ...(['High', 'Medium', 'Low'] as Severity[]).map(
              (s) => [s, SEVERITY_LABELS[s]] as [string, string],
            ),
          ]}
        />
        <FilterSelect
          id="filter-category"
          label="Categoría"
          value={filters.category ?? ALL}
          onChange={(v) => setFilter('category', v)}
          options={[
            [ALL, 'Todas'],
            ...(Object.entries(PROBLEM_CATEGORY_LABELS) as [string, string][]),
          ]}
        />
        <FilterSelect
          id="filter-status"
          label="Estado"
          value={filters.status ?? ALL}
          onChange={(v) => setFilter('status', v)}
          options={[
            [ALL, 'Todos'],
            ...(Object.entries(PROBLEM_STATUS).map(([k, v]) => [k, v.label]) as [string, string][]),
          ]}
        />
      </div>
      <DataTable
        columns={columns}
        data={problems.data?.items ?? []}
        loading={problems.isLoading}
        getRowId={(p) => p.id}
        pageSize={10}
        empty={
          hasFilters ? (
            <EmptyState
              icon={FilterX}
              title="Ningún problema coincide con los filtros"
              action={
                <Button
                  variant="outline"
                  onClick={() => setParams(yearParam ? { year: yearParam } : {}, { replace: true })}
                >
                  Quitar filtros
                </Button>
              }
            />
          ) : (
            <EmptyState
              icon={CircleCheck}
              title="No se detectaron problemas"
              description="Sube tus estados financieros para empezar o elige otro año."
            />
          )
        }
      />
    </div>
  )
}

function FilterSelect({
  id,
  label,
  value,
  onChange,
  options,
}: {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  options: [string, string][]
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger id={id}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {options.map(([v, text]) => (
            <SelectItem key={v} value={v}>
              {text}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  )
}
