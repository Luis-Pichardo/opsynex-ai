import {
  Building2,
  ChartColumn,
  FileUp,
  LayoutDashboard,
  TriangleAlert,
  type LucideIcon,
} from 'lucide-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router'

import { ROLES, hasRole } from '@/auth/permissions'
import { useAuth } from '@/auth/useAuth'
import { EmptyState } from '@/components/common/EmptyState'
import { PageHeader } from '@/components/common/PageHeader'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { useWorkspace } from '@/workspace/useWorkspace'

interface SectionProps {
  title: string
  description: string
  icon: LucideIcon
  emptyTitle: string
  emptyDescription: string
  action?: ReactNode
}

/** Sección cuyo contenido llega en las etapas 09 y 10: encabezado y EmptyState. */
function Section({ title, description, icon, emptyTitle, emptyDescription, action }: SectionProps) {
  const { user } = useAuth()
  const { companyId } = useWorkspace()
  const needsCompany = user?.role === 'Admin' && companyId === null
  return (
    <div className="space-y-6">
      <PageHeader title={title} description={description} />
      <Card>
        {needsCompany ? (
          <EmptyState
            icon={Building2}
            title="Elige una empresa"
            description="Como administrador, selecciona arriba la empresa que quieres consultar."
          />
        ) : (
          <EmptyState
            icon={icon}
            title={emptyTitle}
            description={emptyDescription}
            action={action}
          />
        )}
      </Card>
    </div>
  )
}

function UploadAction() {
  const { user } = useAuth()
  if (!hasRole(user, ROLES.analyze)) return null
  return (
    <Button asChild>
      <Link to="/uploads">
        <FileUp />
        Subir archivo
      </Link>
    </Button>
  )
}

export function DashboardPage() {
  const { user } = useAuth()
  const isAnalyst = hasRole(user, ROLES.analyze)
  return (
    <Section
      title="Dashboard"
      description="Score de salud, impacto económico, KPIs y principales problemas de la empresa."
      icon={LayoutDashboard}
      emptyTitle="Tu dashboard aparecerá aquí"
      emptyDescription={
        isAnalyst
          ? 'Cuando subas el primer archivo contable de la empresa verás el score, los KPIs y los principales problemas.'
          : 'Cuando el analista cargue la información de tu empresa verás aquí el score, los KPIs y los principales problemas.'
      }
      action={<UploadAction />}
    />
  )
}

export function ProblemsPage() {
  return (
    <Section
      title="Problemas"
      description="Ineficiencias detectadas, ordenadas por severidad e impacto económico."
      icon={TriangleAlert}
      emptyTitle="Aún no hay problemas para mostrar"
      emptyDescription="La lista de problemas con su impacto y recomendación estará disponible próximamente."
    />
  )
}

export function KpisPage() {
  return (
    <Section
      title="KPIs"
      description="Indicadores de rentabilidad, costos, productividad y estabilidad financiera por año."
      icon={ChartColumn}
      emptyTitle="Aún no hay KPIs para mostrar"
      emptyDescription="Los indicadores y sus tendencias estarán disponibles próximamente."
    />
  )
}
