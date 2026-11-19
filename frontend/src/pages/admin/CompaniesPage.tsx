import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { ColumnDef } from '@tanstack/react-table'
import { Building2, MoreHorizontal, Pencil, Plus, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { toast } from 'sonner'

import { companiesApi } from '@/api/companies'
import { CompanyFormDialog } from '@/components/admin/CompanyFormDialog'
import { ConfirmDialog } from '@/components/common/ConfirmDialog'
import { EmptyState } from '@/components/common/EmptyState'
import { Button } from '@/components/ui/button'
import { DataTable } from '@/components/ui/data-table'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { EMPTY_VALUE, formatDate } from '@/lib/format'
import { INDUSTRY_LABELS } from '@/lib/labels'
import type { CompanyDto } from '@/types/api'

/** Empresas cliente: listado, alta, edición y eliminación (solo sin usuarios ni datos). */
export function CompaniesPage() {
  const queryClient = useQueryClient()
  const companies = useQuery({
    queryKey: ['companies'],
    queryFn: ({ signal }) => companiesApi.list(signal),
  })
  const [editing, setEditing] = useState<CompanyDto | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [deleting, setDeleting] = useState<CompanyDto | null>(null)

  const remove = useMutation({
    mutationFn: (company: CompanyDto) => companiesApi.remove(company.id),
    onSuccess: async (_, company) => {
      await queryClient.invalidateQueries({ queryKey: ['companies'] })
      toast.success(`Empresa «${company.name}» eliminada.`)
      setDeleting(null)
    },
    onError: () => setDeleting(null),
  })

  const openForm = (company: CompanyDto | null) => {
    setEditing(company)
    setFormOpen(true)
  }

  const columns = useMemo<ColumnDef<CompanyDto, unknown>[]>(
    () => [
      {
        accessorKey: 'name',
        header: 'Nombre',
        cell: ({ row }) => <span className="font-medium text-foreground">{row.original.name}</span>,
      },
      {
        accessorKey: 'industry',
        header: 'Industria',
        cell: ({ row }) => INDUSTRY_LABELS[row.original.industry],
      },
      {
        accessorKey: 'sector',
        header: 'Sector',
        cell: ({ row }) => row.original.sector ?? EMPTY_VALUE,
      },
      { accessorKey: 'currency', header: 'Moneda' },
      {
        accessorKey: 'createdAt',
        header: 'Creada',
        cell: ({ row }) => (
          <span className="tabular-nums">{formatDate(row.original.createdAt)}</span>
        ),
      },
      {
        id: 'actions',
        header: () => <span className="sr-only">Acciones</span>,
        meta: { actions: true },
        enableSorting: false,
        cell: ({ row }) => (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon" aria-label={`Acciones de ${row.original.name}`}>
                <MoreHorizontal />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem onSelect={() => openForm(row.original)}>
                <Pencil aria-hidden="true" />
                Editar
              </DropdownMenuItem>
              <DropdownMenuItem destructive onSelect={() => setDeleting(row.original)}>
                <Trash2 aria-hidden="true" />
                Eliminar
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        ),
      },
    ],
    [],
  )

  return (
    <section className="space-y-4" aria-labelledby="companies-title">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 id="companies-title" className="text-h2 text-foreground">
          Empresas
        </h2>
        <Button onClick={() => openForm(null)}>
          <Plus />
          Nueva empresa
        </Button>
      </div>
      <DataTable
        columns={columns}
        data={companies.data ?? []}
        loading={companies.isLoading}
        getRowId={(c) => c.id}
        empty={
          <EmptyState
            icon={Building2}
            title="Todavía no hay empresas"
            description="Crea la primera empresa cliente para poder asignarle usuarios y subir su información."
          />
        }
      />
      <CompanyFormDialog open={formOpen} onOpenChange={setFormOpen} company={editing} />
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(open) => !open && setDeleting(null)}
        title="Eliminar empresa"
        description={
          <>
            Se eliminará «{deleting?.name}». Solo se puede eliminar una empresa sin usuarios ni
            datos cargados. Esta acción no se puede deshacer.
          </>
        }
        confirmLabel="Eliminar empresa"
        loading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting)}
      />
    </section>
  )
}
