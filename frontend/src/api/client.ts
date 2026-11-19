/**
 * Cliente del API .NET. El frontend solo habla con el API, nunca con el servicio de IA.
 * Agrega el token y la empresa elegida (Admin), convierte los errores ProblemDetails en
 * `ApiError` y avisa del 401 para cerrar la sesión.
 */
import type { ProblemDetails } from '@/types/api'

export const API_URL = (import.meta.env.VITE_API_URL ?? 'http://localhost:5080').replace(/\/$/, '')
export const API_PREFIX = '/api/v1'

export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `Error ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.code = problem.code ?? (status === 0 ? 'network_error' : 'unknown')
    this.problem = problem
  }

  /** Errores de validación por campo (`errors` de ValidationProblemDetails). */
  get fieldErrors(): Record<string, string[]> {
    return this.problem.errors ?? {}
  }
}

interface ClientConfig {
  getToken: () => string | null
  getCompanyId: () => string | null
  onUnauthorized: () => void
}

let config: ClientConfig = {
  getToken: () => null,
  getCompanyId: () => null,
  onUnauthorized: () => {},
}

/** Lo configura `AuthProvider` (token y cierre de sesión) y `WorkspaceProvider` (empresa). */
export function configureApiClient(partial: Partial<ClientConfig>) {
  config = { ...config, ...partial }
}

/** Cabeceras de autenticación y empresa (para peticiones que no usan `apiRequest`, como la subida). */
export function authHeaders(options: { withoutCompany?: boolean } = {}): Record<string, string> {
  const headers: Record<string, string> = {}
  const token = config.getToken()
  if (token) headers.Authorization = `Bearer ${token}`
  const companyId = options.withoutCompany ? null : config.getCompanyId()
  if (companyId) headers['X-Company-Id'] = companyId
  return headers
}

/** Avisa al cliente de un 401 (cierra la sesión). */
export function notifyUnauthorized() {
  config.onUnauthorized()
}

/** Parsea un cuerpo de error del API. */
export function parseProblem(text: string, status: number, statusText = ''): ProblemDetails {
  const data = text ? safeJson(text) : undefined
  return data && typeof data === 'object'
    ? { status, ...(data as ProblemDetails) }
    : { status, title: statusText }
}

export type QueryParams = Record<string, string | number | boolean | null | undefined>

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  query?: QueryParams
  signal?: AbortSignal
  /** No cierra la sesión ante un 401 (p. ej. credenciales incorrectas en el login). */
  skipAuthRedirect?: boolean
  /** No envía la empresa elegida por el Admin (p. ej. en la administración). */
  withoutCompany?: boolean
}

export function buildUrl(path: string, query?: QueryParams): string {
  const url = new URL(
    `${API_URL}${path.startsWith('/api') || path.startsWith('/health') ? '' : API_PREFIX}${path}`,
  )
  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== null && value !== undefined && value !== '')
      url.searchParams.set(key, String(value))
  }
  return url.toString()
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  const token = config.getToken()
  if (token) headers.Authorization = `Bearer ${token}`
  const companyId = options.withoutCompany ? null : config.getCompanyId()
  if (companyId) headers['X-Company-Id'] = companyId
  if (options.body !== undefined) headers['Content-Type'] = 'application/json'

  let response: Response
  try {
    response = await fetch(buildUrl(path, options.query), {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: options.signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new ApiError(0, {
      code: 'network_error',
      title: 'Sin conexión',
      detail: 'No se pudo conectar con el servidor. Revisa tu conexión y vuelve a intentarlo.',
    })
  }

  if (response.status === 204) return undefined as T
  const text = await response.text()
  const data: unknown = text ? safeJson(text) : undefined

  if (!response.ok) {
    const problem: ProblemDetails =
      data && typeof data === 'object' ? (data as ProblemDetails) : { title: response.statusText }
    if (response.status === 401 && token && !options.skipAuthRedirect) config.onUnauthorized()
    throw new ApiError(response.status, { status: response.status, ...problem })
  }
  return data as T
}

function safeJson(text: string): unknown {
  try {
    return JSON.parse(text)
  } catch {
    return undefined
  }
}

/** Mensaje para el usuario a partir de cualquier error (en español). */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const first = Object.values(error.fieldErrors).flat()[0]
    if (error.code === 'validation_failed' && first) return first
    if (error.status === 403) return 'No tienes permiso para realizar esta acción.'
    if (error.status >= 500)
      return 'Ocurrió un error en el servidor. Inténtalo de nuevo en unos minutos.'
    return error.problem.detail ?? error.problem.title ?? error.message
  }
  if (error instanceof Error) return error.message
  return 'Ocurrió un error inesperado.'
}
