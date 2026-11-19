import { useQuery } from '@tanstack/react-query'
import { useLayoutEffect, useMemo, useState, type ReactNode } from 'react'

import { configureApiClient } from '@/api/client'
import { companiesApi } from '@/api/companies'
import { kpisApi } from '@/api/kpis'
import { useAuth } from '@/auth/useAuth'
import { readStorage, writeStorage } from '@/lib/storage'
import { COMPANY_STORAGE_KEY, WorkspaceContext } from './workspace-context'

/** Empresa (Admin) y año seleccionados en la topbar. */
export function WorkspaceProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const isAdmin = user?.role === 'Admin'
  const [selected, setSelected] = useState<string | null>(() => readStorage(COMPANY_STORAGE_KEY))
  const [year, setYear] = useState<number | null>(null)

  const companies = useQuery({
    queryKey: ['companies'],
    queryFn: ({ signal }) => companiesApi.list(signal),
    enabled: user !== null,
  })

  // Si la empresa guardada ya no existe, se ignora.
  const selectedExists = !companies.data || companies.data.some((c) => c.id === selected)
  const adminCompany = selected !== null && selectedExists ? selected : null
  const companyId = isAdmin ? adminCompany : (user?.companyId ?? null)
  const company = companies.data?.find((c) => c.id === companyId)

  // El Admin envía la empresa elegida en cada petición (cabecera X-Company-Id). Efecto de layout
  // para que esté configurada antes de las primeras consultas de las páginas (ver AuthProvider).
  useLayoutEffect(() => {
    configureApiClient({ getCompanyId: () => (isAdmin ? adminCompany : null) })
  }, [isAdmin, adminCompany])

  const kpiYears = useQuery({
    queryKey: ['kpis', 'years', companyId],
    queryFn: ({ signal }) => kpisApi.years(signal),
    enabled: companyId !== null,
    meta: { silentError: true },
  })

  const years = useMemo(
    () => (kpiYears.data?.years ?? []).map((y) => y.year).sort((a, b) => b - a),
    [kpiYears.data],
  )
  const effectiveYear = year !== null && years.includes(year) ? year : (years[0] ?? null)

  const value = useMemo(
    () => ({
      companyId,
      companyName: company?.name ?? user?.companyName ?? null,
      currency: company?.currency ?? kpiYears.data?.currency ?? 'USD',
      companies: companies.data ?? [],
      setCompanyId: (id: string | null) => {
        setSelected(id)
        setYear(null)
        writeStorage(COMPANY_STORAGE_KEY, id)
      },
      years,
      year: effectiveYear,
      setYear,
    }),
    [companyId, company, user, kpiYears.data, companies.data, years, effectiveYear],
  )

  return <WorkspaceContext.Provider value={value}>{children}</WorkspaceContext.Provider>
}
