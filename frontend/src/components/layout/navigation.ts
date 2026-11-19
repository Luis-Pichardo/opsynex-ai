import {
  Building2,
  ChartColumn,
  Factory,
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
}

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
    disabledReason: 'El módulo industrial estará disponible próximamente.',
  },
  { to: '/admin', label: 'Administración', icon: Building2, roles: ROLES.manage },
]

export function navItemsFor(role: UserRole | undefined): NavItem[] {
  return role ? NAV_ITEMS.filter((item) => item.roles.includes(role)) : []
}
