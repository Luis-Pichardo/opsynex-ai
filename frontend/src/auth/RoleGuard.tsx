import { ShieldX } from 'lucide-react'
import type { ReactNode } from 'react'
import { Link, Outlet } from 'react-router'

import { EmptyState } from '@/components/common/EmptyState'
import { Button } from '@/components/ui/button'
import type { UserRole } from '@/types/api'
import { hasRole } from './permissions'
import { useAuth } from './useAuth'

interface RoleGuardProps {
  roles: readonly UserRole[]
  /** Contenido protegido; sin `children` funciona como ruta (renderiza `<Outlet />`). */
  children?: ReactNode
}

/** Muestra el contenido solo a los roles indicados (también si se entra por URL directa). */
export function RoleGuard({ roles, children }: RoleGuardProps) {
  const { user } = useAuth()
  if (!hasRole(user, roles)) {
    return (
      <EmptyState
        icon={ShieldX}
        title="No tienes acceso a esta sección"
        description="Tu rol no permite ver esta página. Si crees que es un error, contacta al administrador."
        action={
          <Button asChild variant="outline">
            <Link to="/">Ir al dashboard</Link>
          </Button>
        }
      />
    )
  }
  return children ?? <Outlet />
}
