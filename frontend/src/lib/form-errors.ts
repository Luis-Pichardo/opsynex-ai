import type { FieldValues, Path, UseFormReturn } from 'react-hook-form'

import { ApiError } from '@/api/client'

/**
 * Lleva los errores de validación del API (`errors` de ValidationProblemDetails) a los campos
 * del formulario. Devuelve `true` si asignó alguno.
 */
export function applyServerErrors<T extends FieldValues>(
  form: UseFormReturn<T>,
  error: unknown,
): boolean {
  if (!(error instanceof ApiError)) return false
  const fields = Object.keys(form.getValues())
  let applied = false
  for (const [key, messages] of Object.entries(error.fieldErrors)) {
    const field = fields.find((f) => f.toLowerCase() === key.toLowerCase())
    if (field && messages[0]) {
      form.setError(field as Path<T>, { type: 'server', message: messages[0] })
      applied = true
    }
  }
  return applied
}
