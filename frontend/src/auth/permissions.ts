import type { UserDto, UserRole } from '@/types/api'

/** Políticas del API (CanManage, CanAnalyze, CanView) expresadas como roles. */
export const ROLES = {
  manage: ['Admin'],
  analyze: ['Admin', 'Analyst'],
  view: ['Admin', 'Analyst', 'Client'],
} as const satisfies Record<string, readonly UserRole[]>

export function hasRole(user: UserDto | null, roles: readonly UserRole[]): boolean {
  return user !== null && roles.includes(user.role)
}

export const ROLE_LABELS: Record<UserRole, string> = {
  Admin: 'Administrador',
  Analyst: 'Analista',
  Client: 'Cliente',
}
