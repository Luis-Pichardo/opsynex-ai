import type { LoginRequest, LoginResponse, UserDto } from '@/types/api'
import { apiRequest } from './client'

export const authApi = {
  login: (request: LoginRequest) =>
    apiRequest<LoginResponse>('/auth/login', {
      method: 'POST',
      body: request,
      skipAuthRedirect: true,
      withoutCompany: true,
    }),
  me: (signal?: AbortSignal) => apiRequest<UserDto>('/auth/me', { signal, withoutCompany: true }),
}
