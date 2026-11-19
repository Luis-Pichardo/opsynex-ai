import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router'

import { industrialApi } from '@/api/industrial'
import type { IndustrialFilter } from '@/types/industrial'
import { useWorkspace } from '@/workspace/useWorkspace'

/**
 * KPIs industriales de la empresa actual con los filtros indicados. Los errores se muestran en
 * la página (filtro inválido, reintentar), no como toast.
 */
export function useIndustrialKpis(filter: IndustrialFilter = {}) {
  const { companyId } = useWorkspace()
  return useQuery({
    queryKey: ['industrial', 'kpis', companyId, filter],
    queryFn: ({ signal }) => industrialApi.kpis(filter, signal),
    enabled: companyId !== null,
    meta: { silentError: true },
  })
}

/** Score industrial de la empresa actual con los filtros indicados. */
export function useIndustrialScore(filter: IndustrialFilter = {}, enabled = true) {
  const { companyId } = useWorkspace()
  return useQuery({
    queryKey: ['industrial', 'score', companyId, filter],
    queryFn: ({ signal }) => industrialApi.score(filter, signal),
    enabled: companyId !== null && enabled,
  })
}

/**
 * ¿La empresa tiene datos industriales? Habilita el menú Producción y el score industrial del
 * dashboard. Comparte la consulta sin filtros con las páginas industriales.
 */
export function useHasIndustrialData() {
  const kpis = useIndustrialKpis()
  return {
    hasData: kpis.data?.hasData ?? false,
    years: kpis.data?.years ?? [],
    isLoading: kpis.isLoading,
  }
}

const toNumber = (value: string | null) => {
  if (!value) return null
  const number = Number(value)
  return Number.isInteger(number) ? number : null
}

/** Filtros de las páginas industriales guardados en la URL (`?year=2024&month=11&lineId=…`). */
export function useIndustrialFilter() {
  const [params, setParams] = useSearchParams()
  const filter: IndustrialFilter = {
    year: toNumber(params.get('year')),
    month: toNumber(params.get('month')),
    lineId: params.get('lineId'),
    machineId: params.get('machineId'),
  }
  const setFilter = (next: IndustrialFilter) => {
    const search = new URLSearchParams()
    for (const [key, value] of Object.entries(next)) {
      if (value !== null && value !== undefined && value !== '') search.set(key, String(value))
    }
    setParams(search, { replace: true })
  }
  return { filter, setFilter }
}
