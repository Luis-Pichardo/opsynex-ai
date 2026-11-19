import { ApiError, errorMessage } from '@/api/client'

/** Mensaje del login según el error del API. */
export function loginErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401) return 'Correo o contraseña incorrectos.'
    if (error.status === 423)
      return error.problem.detail ?? 'Tu cuenta está bloqueada temporalmente. Inténtalo más tarde.'
    if (error.code === 'auth.user_inactive')
      return 'Tu usuario está desactivado. Contacta al administrador.'
  }
  return errorMessage(error)
}
