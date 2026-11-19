import { LogOut } from 'lucide-react'

import { ROLE_LABELS } from '@/auth/permissions'
import { useAuth } from '@/auth/useAuth'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { initials } from '@/lib/utils'

/** Menú de usuario: nombre, correo, rol y cerrar sesión. */
export function UserMenu() {
  const { user, logout } = useAuth()
  if (!user) return null
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className="inline-flex size-10 items-center justify-center rounded-full bg-primary-soft text-small font-semibold text-primary-text transition-colors duration-150 ease-out hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background"
          aria-label={`Menú de ${user.fullName}`}
        >
          {initials(user.fullName)}
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-64">
        <div className="px-2 py-2">
          <p className="truncate text-body font-medium text-foreground">{user.fullName}</p>
          <p className="truncate text-small text-foreground-secondary">{user.email}</p>
        </div>
        <DropdownMenuSeparator />
        <DropdownMenuLabel>Rol</DropdownMenuLabel>
        <p className="px-2 pb-2 text-small text-foreground">{ROLE_LABELS[user.role]}</p>
        <DropdownMenuSeparator />
        <DropdownMenuItem onSelect={() => logout()}>
          <LogOut aria-hidden="true" />
          Cerrar sesión
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
