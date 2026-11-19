import { Building2 } from 'lucide-react'
import type { ReactNode } from 'react'

import { useAuth } from '@/auth/useAuth'
import { Card } from '@/components/ui/card'
import { useWorkspace } from '@/workspace/useWorkspace'
import { EmptyState } from './EmptyState'

/** Para el Admin sin empresa elegida, pide elegirla antes de mostrar el contenido. */
export function RequireCompany({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const { companyId } = useWorkspace()
  if (user?.role === 'Admin' && companyId === null) {
    return (
      <Card>
        <EmptyState
          icon={Building2}
          title="Elige una empresa"
          description="Como administrador, selecciona arriba la empresa con la que quieres trabajar."
        />
      </Card>
    )
  }
  return children
}
