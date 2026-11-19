import { formatNumber } from '@/lib/format'
import type { UploadIssue } from '@/types/uploads'

/** Ubicación legible de un error o advertencia: "Hoja nota1 · fila 18 · columna G". */
export function issueLocation(issue: UploadIssue): string | null {
  const parts = [
    issue.sheet ? `Hoja ${issue.sheet}` : null,
    issue.row ? `fila ${formatNumber(issue.row)}` : null,
    issue.column ? `columna ${issue.column}` : null,
  ].filter(Boolean)
  return parts.length ? parts.join(' · ') : null
}
