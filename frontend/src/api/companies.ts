import type { CompanyDto, CompanyRequest } from '@/types/api'
import { MAX_PAGE_SIZE, apiRequest } from './client'

export const companiesApi = {
  list: (signal?: AbortSignal) =>
    apiRequest<CompanyDto[]>('/companies', {
      query: { pageSize: MAX_PAGE_SIZE },
      signal,
      withoutCompany: true,
    }),
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
