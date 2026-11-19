import type { CreateUserRequest, UpdateUserRequest, UserDto, UserQuery } from '@/types/api'
import { MAX_PAGE_SIZE, apiRequest } from './client'

export const usersApi = {
  list: (query: UserQuery = {}, signal?: AbortSignal) =>
    apiRequest<UserDto[]>('/users', {
      query: { ...query, pageSize: MAX_PAGE_SIZE },
      signal,
      withoutCompany: true,
    }),
  create: (request: CreateUserRequest) =>
    apiRequest<UserDto>('/users', { method: 'POST', body: request, withoutCompany: true }),
  update: (id: string, request: UpdateUserRequest) =>
    apiRequest<UserDto>(`/users/${id}`, { method: 'PUT', body: request, withoutCompany: true }),
  setPassword: (id: string, newPassword: string) =>
    apiRequest<void>(`/users/${id}/password`, {
      method: 'PUT',
      body: { newPassword },
      withoutCompany: true,
    }),
  deactivate: (id: string) =>
    apiRequest<void>(`/users/${id}`, { method: 'DELETE', withoutCompany: true }),
}
