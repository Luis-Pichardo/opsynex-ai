import { clsx, type ClassValue } from 'clsx'
import { extendTailwindMerge } from 'tailwind-merge'

/**
 * tailwind-merge con la escala tipográfica de la guía: sin esto, `text-body` se toma como un
 * color y borra `text-primary-foreground` (el texto del botón quedaba oscuro).
 */
const twMerge = extendTailwindMerge({
  extend: {
    classGroups: {
      'font-size': [
        { text: ['display', 'h1', 'h2', 'h3', 'kpi', 'body', 'small', 'caption', 'overline'] },
      ],
    },
  },
})

/** Combina clases de Tailwind resolviendo conflictos (convención de shadcn/ui). */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/** Iniciales para el avatar: "Ana María Pérez" → "AP". */
export function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  const first = parts[0]?.[0] ?? '?'
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? '') : ''
  return `${first}${last}`.toUpperCase()
}
