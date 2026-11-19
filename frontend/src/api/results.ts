import type {
  AiSummary,
  ImpactSummary,
  ProblemDetail,
  ProblemPage,
  ProblemQuery,
  ProblemStatus,
  ScoreResponse,
} from '@/types/analysis'
import {
  ApiError,
  apiRequest,
  authHeaders,
  buildUrl,
  notifyUnauthorized,
  parseProblem,
} from './client'

export const scoreApi = {
  get: (year?: number | null, signal?: AbortSignal) =>
    apiRequest<ScoreResponse>('/score', { query: { year: year ?? undefined }, signal }),
}

export const impactApi = {
  summary: (year?: number | null, signal?: AbortSignal) =>
    apiRequest<ImpactSummary>('/impact/summary', { query: { year: year ?? undefined }, signal }),
}

export const problemsApi = {
  list: (query: ProblemQuery = {}, signal?: AbortSignal) =>
    apiRequest<ProblemPage>('/problems', { query: { ...query }, signal }),
  get: (id: string, signal?: AbortSignal) =>
    apiRequest<ProblemDetail>(`/problems/${id}`, { signal }),
  setStatus: (id: string, status: ProblemStatus) =>
    apiRequest<ProblemDetail>(`/problems/${id}`, { method: 'PATCH', body: { status } }),
}

export const aiApi = {
  summary: (year?: number | null, signal?: AbortSignal) =>
    apiRequest<AiSummary>('/ai/summary', { query: { year: year ?? undefined }, signal }),
  /** Vuelve a generar el resumen y las recomendaciones con IA (202: se genera en segundo plano). */
  regenerate: (year?: number | null) =>
    apiRequest<AiSummary>('/ai/regenerate', { method: 'POST', query: { year: year ?? undefined } }),
}

/** Descarga el informe ejecutivo en PDF (todos los roles) y lo guarda en el equipo. */
export async function downloadExecutiveReport(year?: number | null): Promise<void> {
  const response = await fetch(buildUrl('/reports/executive', { year: year ?? undefined }), {
    headers: { Accept: 'application/pdf', ...authHeaders() },
  })
  if (!response.ok) {
    if (response.status === 401) notifyUnauthorized()
    throw new ApiError(
      response.status,
      parseProblem(await response.text(), response.status, response.statusText),
    )
  }
  const disposition = response.headers.get('Content-Disposition') ?? ''
  const match = /filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i.exec(disposition)
  const fileName = decodeURIComponent(
    match?.[1] ?? match?.[2] ?? `informe_ejecutivo_${year ?? ''}.pdf`,
  )
  const url = URL.createObjectURL(await response.blob())
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}
