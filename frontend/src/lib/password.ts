import { z } from 'zod'

/** Mismas reglas que el API (PasswordRules): mínimo 8 caracteres, con letras y números. */
export const passwordSchema = z
  .string()
  .min(8, 'La contraseña debe tener al menos 8 caracteres.')
  .max(128, 'Máximo 128 caracteres.')
  .regex(/\p{L}/u, 'La contraseña debe incluir al menos una letra.')
  .regex(/\d/, 'La contraseña debe incluir al menos un número.')
