import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { ColumnDef } from '@tanstack/react-table'
import { KeyRound, MoreHorizontal, Pencil, Plus, Search, UserX, Users } from 'lucide-react'
import { useDeferredValue, useMemo, useState } from 'react'
import { toast } from 'sonner'

import { companiesApi } from '@/api/companies'
import { usersApi } from '@/api/users'
import { ROLE_LABELS } from '@/auth/permissions'
import { useAuth } from '@/auth/useAuth'
import { PasswordDialog } from '@/components/admin/PasswordDialog'
import { UserFormDialog } from '@/components/admin/UserFormDialog'
import { ConfirmDialog } from '@/components/common/ConfirmDialog'
import { EmptyState } from '@/components/common/EmptyState'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DataTable } from '@/components/ui/data-table'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { EMPTY_VALUE, formatDate } from '@/lib/format'
import type { UserDto, UserRole } from '@/types/api'

const ALL = 'all'

/** Usuarios: listado con filtros, alta, edición, cambio de contraseña y desactivación. */
export function UsersPage() {
  const { user: me } = useAuth()
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const deferredSearch = useDeferredValue(search.trim())
  const [role, setRole] = useState<string>(ALL)
  const [companyId, setCompanyId] = useState<string>(ALL)
  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<UserDto | null>(null)
  const [passwordFor, setPasswordFor] = useState<UserDto | null>(null)
  const [deactivating, setDeactivating] = useState<UserDto | null>(null)

  const companies = useQuery({
    queryKey: ['companies'],
    queryFn: ({ signal }) => companiesApi.list(signal),
  })
  const query = {
    search: deferredSearch || undefined,
    role: role === ALL ? undefined : (role as UserRole),
    companyId: companyId === ALL ? undefined : companyId,
  }
  const users = useQuery({
    queryKey: ['users', query],
    queryFn: ({ signal }) => usersApi.list(query, signal),
    placeholderData: (previous) => previous,
  })

  const deactivate = useMutation({
    mutationFn: (user: UserDto) => usersApi.deactivate(user.id),
    onSuccess: async (_, user) => {
      await queryClient.invalidateQueries({ queryKey: ['users'] })
      toast.success(`Usuario «${user.fullName}» desactivado.`)
      setDeactivating(null)
    },
    onError: () => setDeactivating(null),
  })

  const openForm = (user: UserDto | null) => {
    setEditing(user)
    setFormOpen(true)
  }

  const columns = useMemo<ColumnDef<UserDto, unknown>[]>(
    () => [
      {
        accessorKey: 'fullName',
        header: 'Nombre',
        cell: ({ row }) => (
          <div className="min-w-48">
            <p className="font-medium text-foreground">{row.original.fullName}</p>
            <p className="text-small text-foreground-secondary">{row.original.email}</p>
          </div>
        ),
      },
      { accessorKey: 'role', header: 'Rol', cell: ({ row }) => ROLE_LABELS[row.original.role] },
      {
        accessorKey: 'companyName',
        header: 'Empresa',
        cell: ({ row }) => row.original.companyName ?? EMPTY_VALUE,
      },
      {
        accessorKey: 'isActive',
        header: 'Estado',
        cell: ({ row }) =>
          row.original.isActive ? (
            <Badge variant="good">Activo</Badge>
          ) : (
            <Badge variant="neutral">Inactivo</Badge>
          ),
      },
      {
        accessorKey: 'lastLoginAt',
        header: 'Último acceso',
        cell: ({ row }) => (
          <span className="whitespace-nowrap tabular-nums">
            {row.original.lastLoginAt
              ? formatDate(row.original.lastLoginAt, { withTime: true })
              : 'Nunca'}
          </span>
        ),
      },
      {
        id: 'actions',
        header: () => <span className="sr-only">Acciones</span>,
        meta: { actions: true },
        enableSorting: false,
        cell: ({ row }) => {
          const isMe = row.original.id === me?.id
          return (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Acciones de ${row.original.fullName}`}
                >
                  <MoreHorizontal />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem onSelect={() => openForm(row.original)}>
                  <Pencil aria-hidden="true" />
                  Editar
                </DropdownMenuItem>
                <DropdownMenuItem onSelect={() => setPasswordFor(row.original)}>
                  <KeyRound aria-hidden="true" />
                  Cambiar contraseña
                </DropdownMenuItem>
                {row.original.isActive && !isMe ? (
                  <>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem destructive onSelect={() => setDeactivating(row.original)}>
                      <UserX aria-hidden="true" />
                      Desactivar
                    </DropdownMenuItem>
                  </>
                ) : null}
              </DropdownMenuContent>
            </DropdownMenu>
          )
        },
      },
    ],
    [me?.id],
  )

  const hasFilters =
    query.search !== undefined || query.role !== undefined || query.companyId !== undefined

  return (
    <section className="space-y-4" aria-labelledby="users-title">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 id="users-title" className="text-h2 text-foreground">
          Usuarios
        </h2>
        <Button onClick={() => openForm(null)}>
          <Plus />
          Nuevo usuario
        </Button>
      </div>
      <div className="grid gap-3 sm:grid-cols-3">
        <div className="relative">
          <Search
            className="pointer-events-none absolute left-3 top-3 size-4 text-foreground-muted"
            aria-hidden="true"
          />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Buscar por nombre o correo"
            aria-label="Buscar usuarios"
            className="pl-9"
          />
        </div>
        <Select value={role} onValueChange={setRole}>
          <SelectTrigger aria-label="Filtrar por rol">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Todos los roles</SelectItem>
            {(Object.keys(ROLE_LABELS) as UserRole[]).map((r) => (
              <SelectItem key={r} value={r}>
                {ROLE_LABELS[r]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={companyId} onValueChange={setCompanyId}>
          <SelectTrigger aria-label="Filtrar por empresa">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Todas las empresas</SelectItem>
            {(companies.data ?? []).map((c) => (
              <SelectItem key={c.id} value={c.id}>
                {c.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <DataTable
        columns={columns}
        data={users.data ?? []}
        loading={users.isLoading}
        getRowId={(u) => u.id}
        empty={
          <EmptyState
            icon={Users}
            title={
              hasFilters ? 'Ningún usuario coincide con los filtros' : 'Todavía no hay usuarios'
            }
            description={
              hasFilters
                ? 'Prueba con otro nombre, rol o empresa.'
                : 'Crea el primer usuario para darle acceso a una empresa.'
            }
          />
        }
      />
      <UserFormDialog
        open={formOpen}
        onOpenChange={setFormOpen}
        companies={companies.data ?? []}
        user={editing}
      />
      <PasswordDialog user={passwordFor} onOpenChange={(open) => !open && setPasswordFor(null)} />
      <ConfirmDialog
        open={deactivating !== null}
        onOpenChange={(open) => !open && setDeactivating(null)}
        title="Desactivar usuario"
        description={
          <>
            «{deactivating?.fullName}» no podrá iniciar sesión. Puedes volver a activarlo desde
            «Editar».
          </>
        }
        confirmLabel="Desactivar usuario"
        loading={deactivate.isPending}
        onConfirm={() => deactivating && deactivate.mutate(deactivating)}
      />
    </section>
  )
}
