/** Tipos del API .NET (`/api/v1`). Los enums viajan como texto. */

export type UserRole = 'Admin' | 'Analyst' | 'Client'
export type Industry = 'Manufacturing' | 'Services'

export interface UserDto {
  id: string
  email: string
  fullName: string
  role: UserRole
  companyId: string | null
  companyName: string | null
  isActive: boolean
  lastLoginAt: string | null
  createdAt: string
}

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  tokenType: string
  expiresAt: string
  user: UserDto
}

export interface CompanyDto {
  id: string
  name: string
  industry: Industry
  sector: string | null
  currency: string
  createdAt: string
  updatedAt: string
}

export interface CompanyRequest {
  name: string
  industry: Industry
  sector: string | null
  currency: string | null
}

export interface CreateUserRequest {
  email: string
  password: string
  fullName: string
  role: UserRole
  companyId: string | null
}

export interface UpdateUserRequest {
  email: string
  fullName: string
  role: UserRole
  companyId: string | null
  isActive: boolean
}

export interface UserQuery {
  companyId?: string
  role?: UserRole
  search?: string
}

/** Error RFC 7807 del API con el `code` propio de Opsynex. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  code?: string
  traceId?: string
  errors?: Record<string, string[]>
}

/** Respuesta mínima de `/kpis` que usa el selector de año (el resto llega en la etapa 10). */
export interface KpiYearsResponse {
  companyId: string
  currency: string
  years: { year: number }[]
}
