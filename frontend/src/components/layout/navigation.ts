import {
  Building2,
  ChartColumn,
  Factory,
  Coins,
  FileUp,
  GitMerge,
  LayoutDashboard,
  TriangleAlert,
  type LucideIcon,
} from 'lucide-react'

import { ROLES } from '@/auth/permissions'
import type { UserRole } from '@/types/api'

export interface NavItem {
  to: string
  label: string
  icon: LucideIcon
  roles: readonly UserRole[]
  /** Visible pero no disponible todavía (se explica en un tooltip). */
  disabledReason?: string
  /** Solo se habilita si la empresa tiene datos industriales (producción, costos, estándares). */
  requiresIndustrial?: boolean
}

/** Motivo del ítem deshabilitado cuando la empresa no tiene datos industriales. */
export const INDUSTRIAL_DISABLED_REASON =
  'Se habilita cuando la empresa tiene datos de producción. Sube las plantillas de producción, costos y estándares.'

/** Menú principal (GUIA_DE_DISENO.md §5). Solo se muestra lo que el rol permite. */
export const NAV_ITEMS: NavItem[] = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, roles: ROLES.view },
  { to: '/problems', label: 'Problemas', icon: TriangleAlert, roles: ROLES.view },
  { to: '/kpis', label: 'KPIs', icon: ChartColumn, roles: ROLES.view },
  { to: '/uploads', label: 'Carga de datos', icon: FileUp, roles: ROLES.analyze },
  { to: '/mappings', label: 'Homologación', icon: GitMerge, roles: ROLES.analyze },
  {
    to: '/production',
    label: 'Producción',
    icon: Factory,
    roles: ROLES.view,
    requiresIndustrial: true,
  },
  { to: '/costs', label: 'Costos', icon: Coins, roles: ROLES.view, requiresIndustrial: true },
  { to: '/admin', label: 'Administración', icon: Building2, roles: ROLES.manage },
]

/**
 * Ítems del menú para el rol. Los del módulo industrial se muestran deshabilitados (con su
 * motivo) mientras la empresa no tenga datos industriales.
 */
export function navItemsFor(role: UserRole | undefined, hasIndustrialData = false): NavItem[] {
  if (!role) return []
  return NAV_ITEMS.filter((item) => item.roles.includes(role)).map((item) =>
    item.requiresIndustrial && !hasIndustrialData
      ? { ...item, disabledReason: INDUSTRIAL_DISABLED_REASON }
      : item,
  )
}
