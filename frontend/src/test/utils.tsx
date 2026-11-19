import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactElement, ReactNode } from 'react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { vi } from 'vitest'

import { SESSION_STORAGE_KEY, type Session } from '@/auth/auth-context'
import { AuthProvider } from '@/auth/AuthProvider'
import { Toaster } from '@/components/ui/sonner'
import { TooltipProvider } from '@/components/ui/tooltip'
import { buildRoutes } from '@/router/routes'
import { ThemeProvider } from '@/theme/ThemeProvider'
import type { UserDto, UserRole } from '@/types/api'
import { WorkspaceProvider } from '@/workspace/WorkspaceProvider'

/** QueryClient de pruebas: sin reintentos. */
export function testQueryClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
}

/** Renderiza con un QueryClient nuevo y sin reintentos. */
export function renderWithQuery(ui: ReactElement) {
  return render(<QueryClientProvider client={testQueryClient()}>{ui}</QueryClientProvider>)
}

export const COMPANY_ID = '0196f000-0000-7000-8000-000000000001'

export function makeUser(role: UserRole, overrides: Partial<UserDto> = {}): UserDto {
  return {
    id: `0196f000-0000-7000-8000-00000000010${role.length}`,
    email: `${role.toLowerCase()}@demo.test`,
    fullName: `Usuario ${role}`,
    role,
    companyId: role === 'Admin' ? null : COMPANY_ID,
    companyName: role === 'Admin' ? null : 'Empresa Demo',
    isActive: true,
    lastLoginAt: null,
    createdAt: '2026-11-01T10:00:00Z',
    ...overrides,
  }
}

/** Guarda una sesión válida (o vencida) en localStorage, como si el usuario ya hubiera entrado. */
export function storeSession(user: UserDto, expiresInMs = 60 * 60 * 1000): Session {
  const session: Session = {
    token: `token-${user.role}`,
    expiresAt: new Date(Date.now() + expiresInMs).toISOString(),
    user,
  }
  window.localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session))
  return session
}

type Handler = (request: { url: URL; init: RequestInit }) => unknown

/**
 * Simula el API: `handlers` por "MÉTODO /ruta" (sin query). Un handler devuelve el cuerpo JSON
 * o un `Response`. Las rutas sin handler responden 404 ProblemDetails.
 */
export function mockApi(handlers: Record<string, Handler | unknown>) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init: RequestInit = {}) => {
    const url = new URL(typeof input === 'string' ? input : input.toString())
    const key = `${init.method ?? 'GET'} ${url.pathname}`
    if (!(key in handlers)) {
      return json({ title: 'Not Found', code: 'not_found', status: 404 }, 404)
    }
    const handler = handlers[key]
    const result = typeof handler === 'function' ? (handler as Handler)({ url, init }) : handler
    return result instanceof Response ? result : json(result)
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

export function json(body: unknown, status = 200) {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

/** Respuestas por defecto que usan el layout (empresas y años). */
export const layoutHandlers = {
  'GET /api/v1/companies': [
    {
      id: COMPANY_ID,
      name: 'Empresa Demo',
      industry: 'Manufacturing',
      sector: null,
      currency: 'DOP',
      createdAt: '2026-11-01T10:00:00Z',
      updatedAt: '2026-11-01T10:00:00Z',
    },
  ],
  'GET /api/v1/kpis': {
    companyId: COMPANY_ID,
    currency: 'DOP',
    years: [{ year: 2023 }, { year: 2024 }],
  },
}

function Providers({ children }: { children: ReactNode }) {
  return (
    <ThemeProvider>
      <QueryClientProvider client={testQueryClient()}>
        <AuthProvider>
          <WorkspaceProvider>
            <TooltipProvider>
              {children}
              <Toaster />
            </TooltipProvider>
          </WorkspaceProvider>
        </AuthProvider>
      </QueryClientProvider>
    </ThemeProvider>
  )
}

/** Renderiza la app completa (rutas reales) en la ruta indicada. */
export function renderApp(path = '/') {
  const router = createMemoryRouter(buildRoutes({ includeDesign: true }), {
    initialEntries: [path],
  })
  const result = render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  )
  return { ...result, router }
}

export { Providers }
