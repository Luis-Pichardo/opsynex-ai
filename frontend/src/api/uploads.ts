import type { UploadFileType, UploadListItem, UploadReport } from '@/types/uploads'
import {
  ApiError,
  apiRequest,
  authHeaders,
  buildUrl,
  MAX_PAGE_SIZE,
  notifyUnauthorized,
  parseProblem,
} from './client'

export interface UploadOptions {
  /** Tipo de plantilla (solo para un CSV sin título). */
  fileType?: UploadFileType
  /** Progreso del envío del archivo, de 0 a 1. */
  onProgress?: (fraction: number) => void
  signal?: AbortSignal
}

/**
 * Sube un archivo con `XMLHttpRequest` (fetch no informa el progreso de envío). El API devuelve
 * 201 con el reporte o 422 con el reporte y `status: "Failed"`: ambos se devuelven como reporte.
 */
export function uploadFile(file: File, options: UploadOptions = {}): Promise<UploadReport> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest()
    xhr.open('POST', buildUrl('/uploads'))
    xhr.setRequestHeader('Accept', 'application/json')
    for (const [name, value] of Object.entries(authHeaders())) xhr.setRequestHeader(name, value)

    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) options.onProgress?.(event.loaded / event.total)
    }
    xhr.onload = () => {
      const text = xhr.responseText
      if (xhr.status === 201 || xhr.status === 422) {
        try {
          resolve(JSON.parse(text) as UploadReport)
          return
        } catch {
          // Respuesta inesperada: se trata como error abajo.
        }
      }
      if (xhr.status === 401) notifyUnauthorized()
      reject(new ApiError(xhr.status, parseProblem(text, xhr.status, xhr.statusText)))
    }
    xhr.onerror = () =>
      reject(
        new ApiError(0, {
          code: 'network_error',
          title: 'Sin conexión',
          detail: 'No se pudo enviar el archivo. Revisa tu conexión y vuelve a intentarlo.',
        }),
      )
    xhr.onabort = () => reject(new DOMException('Carga cancelada', 'AbortError'))
    options.signal?.addEventListener('abort', () => xhr.abort())

    const form = new FormData()
    form.append('file', file)
    if (options.fileType) form.append('fileType', options.fileType)
    const companyId = authHeaders()['X-Company-Id']
    if (companyId) form.append('companyId', companyId)
    xhr.send(form)
  })
}

export const uploadsApi = {
  list: (signal?: AbortSignal) =>
    apiRequest<UploadListItem[]>('/uploads', { query: { pageSize: MAX_PAGE_SIZE }, signal }),
  get: (id: string, signal?: AbortSignal) => apiRequest<UploadReport>(`/uploads/${id}`, { signal }),
}

/** Descarga una plantilla `.xlsx` (con autenticación) y la guarda en el equipo. */
export async function downloadTemplate(fileType: UploadFileType): Promise<void> {
  const response = await fetch(buildUrl(`/uploads/templates/${fileType}`), {
    headers: authHeaders(),
  })
  if (!response.ok) {
    if (response.status === 401) notifyUnauthorized()
    throw new ApiError(
      response.status,
      parseProblem(await response.text(), response.status, response.statusText),
    )
  }
  const blob = await response.blob()
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `plantilla_${fileType}.xlsx`
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}
