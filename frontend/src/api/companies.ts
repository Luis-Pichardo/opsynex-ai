import type { CompanyDto, CompanyRequest } from '@/types/api'
import { apiRequest } from './client'

export const companiesApi = {
  list: (signal?: AbortSignal) =>
    apiRequest<CompanyDto[]>('/companies', { signal, withoutCompany: true }),
  create: (request: CompanyRequest) =>
    apiRequest<CompanyDto>('/companies', { method: 'POST', body: request, withoutCompany: true }),
  update: (id: string, request: CompanyRequest) =>
    apiRequest<CompanyDto>(`/companies/${id}`, {
      method: 'PUT',
      body: request,
      withoutCompany: true,
    }),
  remove: (id: string) =>
    apiRequest<void>(`/companies/${id}`, { method: 'DELETE', withoutCompany: true }),
}
