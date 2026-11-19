import { useQueryClient } from '@tanstack/react-query'
import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import { toast } from 'sonner'

import { authApi } from '@/api/auth'
import { configureApiClient } from '@/api/client'
import { AuthContext, type LogoutReason, type Session } from './auth-context'
import { loadSession, saveSession } from './session'

/** Máximo que admite setTimeout (~24,8 días). */
const MAX_TIMEOUT = 2_147_483_647

/**
 * Sesión con JWT: guarda el token en el navegador, lo envía en cada petición y cierra la
 * sesión al vencer el token o cuando el API responde 401.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [session, setSession] = useState<Session | null>(() => loadSession())
  const tokenRef = useRef<string | null>(session?.token ?? null)

  const logout = useCallback(
    (reason: LogoutReason = 'user') => {
      if (!tokenRef.current) return
      tokenRef.current = null
      saveSession(null)
      setSession(null)
      queryClient.clear()
      if (reason === 'expired') {
        toast.info('Tu sesión expiró. Inicia sesión de nuevo.', { id: 'session-expired' })
      }
    },
    [queryClient],
  )

  // En un efecto de layout: todos corren antes que los efectos normales de los hijos, donde
  // TanStack Query lanza sus peticiones. Con useEffect, al recargar la página las primeras
  // consultas salían sin token (los efectos de los hijos corren antes que los del padre).
  useLayoutEffect(() => {
    configureApiClient({
      getToken: () => tokenRef.current,
      onUnauthorized: () => logout('expired'),
    })
  }, [logout])

  // Cierra la sesión cuando vence el token.
  useEffect(() => {
    if (!session) return
    const remaining = Date.parse(session.expiresAt) - Date.now()
    const timer = window.setTimeout(
      () => logout('expired'),
      Math.min(Math.max(remaining, 0), MAX_TIMEOUT),
    )
    return () => window.clearTimeout(timer)
  }, [session, logout])

  // Al abrir la app con una sesión guardada, actualiza el usuario (rol o empresa pudieron cambiar).
  const token = session?.token
  useEffect(() => {
    if (!token) return
    const controller = new AbortController()
    authApi
      .me(controller.signal)
      .then((user) =>
        setSession((current) => {
          if (!current || current.token !== token) return current
          const next = { ...current, user }
          saveSession(next)
          return next
        }),
      )
      .catch(() => {
        // Un 401 ya cerró la sesión desde el cliente; otros errores no la invalidan.
      })
    return () => controller.abort()
  }, [token])

  const login = useCallback(
    async (email: string, password: string) => {
      const response = await authApi.login({ email, password })
      const next: Session = {
        token: response.accessToken,
        expiresAt: response.expiresAt,
        user: response.user,
      }
      queryClient.clear()
      tokenRef.current = next.token
      saveSession(next)
      setSession(next)
      return response.user
    },
    [queryClient],
  )

  const value = useMemo(
    () => ({
      user: session?.user ?? null,
      token: session?.token ?? null,
      isAuthenticated: session !== null,
      login,
      logout,
    }),
    [session, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
