import { createContext } from 'react'

import type { CompanyDto } from '@/types/api'

export interface WorkspaceContextValue {
  /** Empresa sobre la que se trabaja: la del usuario o la que eligió el Admin. */
  companyId: string | null
  companyName: string | null
  currency: string
  /** Empresas disponibles (solo para el Admin). */
  companies: CompanyDto[]
  setCompanyId: (id: string | null) => void
  /** Años con análisis de la empresa (más reciente primero). */
  years: number[]
  /** Año elegido; por defecto el más reciente. */
  year: number | null
  setYear: (year: number | null) => void
}

export const WorkspaceContext = createContext<WorkspaceContextValue | null>(null)

export const COMPANY_STORAGE_KEY = 'opsynex.company'
