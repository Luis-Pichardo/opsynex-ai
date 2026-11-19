import { act, renderHook, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { apiRequest } from '@/api/client'
import { json, makeUser, mockApi, Providers, storeSession } from '@/test/utils'
import { SESSION_STORAGE_KEY } from './auth-context'
import { useAuth } from './useAuth'

const analyst = makeUser('Analyst')

function renderAuth() {
  return renderHook(() => useAuth(), { wrapper: Providers })
}

describe('AuthContext', () => {
  afterEach(() => vi.useRealTimers())

  it('inicia sesión, guarda el token y envía la cabecera Authorization', async () => {
    const fetchMock = mockApi({
      'POST /api/v1/auth/login': {
        accessToken: 'jwt-123',
        tokenType: 'Bearer',
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
        user: analyst,
      },
      'GET /api/v1/auth/me': analyst,
      'GET /api/v1/companies': [],
      'GET /api/v1/kpis': { companyId: analyst.companyId, currency: 'DOP', years: [] },
    })
    const { result } = renderAuth()
    expect(result.current.isAuthenticated).toBe(false)

    await act(() => result.current.login('analyst@demo.test', 'Clave1234'))

    expect(result.current.isAuthenticated).toBe(true)
    expect(result.current.user?.role).toBe('Analyst')
    expect(JSON.parse(window.localStorage.getItem(SESSION_STORAGE_KEY)!).token).toBe('jwt-123')

    await act(() => apiRequest('/kpis'))
    const lastCall = fetchMock.mock.calls.at(-1)!
    expect((lastCall[1]!.headers as Record<string, string>).Authorization).toBe('Bearer jwt-123')
  })

  it('cierra sesión y borra el token', async () => {
    mockApi({
      'GET /api/v1/auth/me': analyst,
      'GET /api/v1/companies': [],
      'GET /api/v1/kpis': { years: [] },
    })
    storeSession(analyst)
    const { result } = renderAuth()
    expect(result.current.isAuthenticated).toBe(true)

    act(() => result.current.logout())

    expect(result.current.isAuthenticated).toBe(false)
    expect(window.localStorage.getItem(SESSION_STORAGE_KEY)).toBeNull()
  })

  it('ignora una sesión guardada que ya venció', () => {
    mockApi({})
    storeSession(analyst, -1000)
    const { result } = renderAuth()
    expect(result.current.isAuthenticated).toBe(false)
    expect(window.localStorage.getItem(SESSION_STORAGE_KEY)).toBeNull()
  })

  it('cierra la sesión cuando el API responde 401 (token vencido o revocado)', async () => {
    mockApi({
      'GET /api/v1/auth/me': () =>
        json({ title: 'Unauthorized', code: 'auth.token_expired', status: 401 }, 401),
      'GET /api/v1/companies': [],
    })
    storeSession(analyst)
    const { result } = renderAuth()

    await waitFor(() => expect(result.current.isAuthenticated).toBe(false))
    expect(window.localStorage.getItem(SESSION_STORAGE_KEY)).toBeNull()
  })

  it('cierra la sesión al llegar la hora de vencimiento del token', () => {
    vi.useFakeTimers()
    mockApi({
      'GET /api/v1/auth/me': analyst,
      'GET /api/v1/companies': [],
      'GET /api/v1/kpis': { years: [] },
    })
    storeSession(analyst, 5_000)
    const { result } = renderAuth()
    expect(result.current.isAuthenticated).toBe(true)

    act(() => vi.advanceTimersByTime(5_001))

    expect(result.current.isAuthenticated).toBe(false)
  })

  it('un 401 del login no cierra otra sesión: muestra credenciales incorrectas', async () => {
    mockApi({
      'POST /api/v1/auth/login': () =>
        json({ title: 'Unauthorized', code: 'auth.invalid_credentials', status: 401 }, 401),
    })
    const { result } = renderAuth()
    await expect(act(() => result.current.login('x@demo.test', 'mala1234'))).rejects.toMatchObject({
      status: 401,
      code: 'auth.invalid_credentials',
    })
    expect(result.current.isAuthenticated).toBe(false)
  })
})
