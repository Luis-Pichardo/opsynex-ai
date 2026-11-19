import { Factory, FileUp } from 'lucide-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router'

import { ApiError } from '@/api/client'
import { ROLES, hasRole } from '@/auth/permissions'
import { useAuth } from '@/auth/useAuth'
import { EmptyState } from '@/components/common/EmptyState'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useIndustrialFilter, useIndustrialKpis } from '@/industrial/useIndustrial'
import type { IndustrialFilter, IndustrialKpisResponse } from '@/types/industrial'
import { IndustrialFilters } from './IndustrialFilters'

/** Carga, filtros, errores y estado vacío comunes a Producción y Costos. */
export function IndustrialPage({
  render,
}: {
  render: (data: IndustrialKpisResponse, filter: IndustrialFilter) => ReactNode
}) {
  const { user } = useAuth()
  const { filter, setFilter } = useIndustrialFilter()
  const kpis = useIndustrialKpis(filter)

  if (kpis.isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-16" />
        <div className="grid gap-4 lg:grid-cols-12">
          <Skeleton className="h-80 lg:col-span-5" />
          <Skeleton className="h-80 lg:col-span-7" />
        </div>
      </div>
    )
  }

  if (kpis.isError) {
    const invalidFilter = kpis.error instanceof ApiError && kpis.error.status === 400
    return (
      <Card>
        <EmptyState
          icon={Factory}
          title={
            invalidFilter ? 'No hay datos con esos filtros' : 'No se pudieron cargar los datos'
          }
          description={kpis.error.message}
          action={
            invalidFilter ? (
              <Button variant="outline" onClick={() => setFilter({})}>
                Quitar filtros
              </Button>
            ) : (
              <Button variant="outline" onClick={() => void kpis.refetch()}>
                Reintentar
              </Button>
            )
          }
        />
      </Card>
    )
  }

  const data = kpis.data
  if (!data || !data.hasData) {
    const canUpload = hasRole(user, ROLES.analyze)
    return (
      <Card>
        <EmptyState
          icon={Factory}
          title="Todavía no hay datos de producción"
          description={
            canUpload
              ? 'Sube las plantillas de producción diaria, costos y estándares por máquina para ver el OEE, el scrap, las paradas y el costo por unidad.'
              : 'Cuando el analista cargue la producción de tu empresa verás aquí el OEE, el scrap, las paradas y los costos por máquina.'
          }
          action={
            canUpload ? (
              <Button asChild>
                <Link to="/uploads">
                  <FileUp />
                  Subir datos de producción
                </Link>
              </Button>
            ) : null
          }
        />
      </Card>
    )
  }

  return (
    <div className="space-y-4 lg:space-y-6">
      <IndustrialFilters data={data} filter={filter} onChange={setFilter} />
      {data.warnings.length > 0 ? (
        <ul
          className="space-y-1 rounded border border-info bg-info-soft p-3 text-small text-info-text"
          aria-label="Datos incompletos"
        >
          {data.warnings.map((warning) => (
            <li key={warning}>{warning}</li>
          ))}
        </ul>
      ) : null}
      {render(data, filter)}
    </div>
  )
}
