import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

import { cn } from '@/lib/utils'

interface EmptyStateProps {
  icon: LucideIcon
  title: string
  description?: ReactNode
  action?: ReactNode
  className?: string
}

/** EmptyState (GUIA_DE_DISENO.md §6): icono de 40 px, título `h3`, descripción `small` y acción. */
export function EmptyState({ icon: Icon, title, description, action, className }: EmptyStateProps) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-3 px-4 py-12 text-center sm:py-16',
        className,
      )}
    >
      <Icon className="size-10 text-foreground-muted" aria-hidden="true" strokeWidth={1.5} />
      <h3 className="text-h3 text-foreground">{title}</h3>
      {description ? (
        <p className="max-w-prose text-small text-foreground-secondary">{description}</p>
      ) : null}
      {action ? <div className="mt-2">{action}</div> : null}
    </div>
  )
}
