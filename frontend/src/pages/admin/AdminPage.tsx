import { Building2, Users } from 'lucide-react'
import { NavLink, Outlet } from 'react-router'

import { PageHeader } from '@/components/common/PageHeader'
import { cn } from '@/lib/utils'

const TABS = [
  { to: '/admin/companies', label: 'Empresas', icon: Building2 },
  { to: '/admin/users', label: 'Usuarios', icon: Users },
]

/** Administración (solo Admin): empresas y usuarios, en pestañas con su propia URL. */
export function AdminPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Administración"
        description="Crea las empresas cliente y los usuarios que pueden acceder a cada una."
      />
      <nav
        aria-label="Secciones de administración"
        className="flex gap-6 overflow-x-auto border-b border-border"
      >
        {TABS.map(({ to, label, icon: Icon }) => (
          <NavLink
            key={to}
            to={to}
            className={({ isActive }) =>
              cn(
                '-mb-px inline-flex h-11 items-center gap-2 whitespace-nowrap border-b-2 text-body font-medium transition-colors duration-150 ease-out focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                isActive
                  ? 'border-primary text-foreground'
                  : 'border-transparent text-foreground-secondary hover:text-foreground',
              )
            }
          >
            <Icon className="size-4" aria-hidden="true" />
            {label}
          </NavLink>
        ))}
      </nav>
      <Outlet />
    </div>
  )
}
