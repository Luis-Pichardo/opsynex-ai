/** Reglas de archivo que el cliente valida antes de enviar (las mismas que el API). */
export const MAX_UPLOAD_MB = 20
export const ACCEPTED_EXTENSIONS = ['.xls', '.xlsx', '.csv'] as const

export type FileCheck = { ok: true; extension: string } | { ok: false; message: string }

export function checkFile(file: File, maxMb = MAX_UPLOAD_MB): FileCheck {
  const dot = file.name.lastIndexOf('.')
  const extension = dot >= 0 ? file.name.slice(dot).toLowerCase() : ''
  if (!(ACCEPTED_EXTENSIONS as readonly string[]).includes(extension)) {
    return {
      ok: false,
      message: `El archivo «${file.name}» no es .xls, .xlsx ni .csv. Sube el libro contable de Excel o una plantilla.`,
    }
  }
  if (file.size === 0) return { ok: false, message: `El archivo «${file.name}» está vacío.` }
  if (file.size > maxMb * 1024 * 1024) {
    return {
      ok: false,
      message: `El archivo supera el máximo de ${maxMb} MB. Quita hojas que no sean financieras o súbelo por partes.`,
    }
  }
  return { ok: true, extension }
}
