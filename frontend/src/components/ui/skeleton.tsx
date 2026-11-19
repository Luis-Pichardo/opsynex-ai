import type * as React from 'react'

import { cn } from '@/lib/utils'

/** Skeleton (GUIA_DE_DISENO.md §6): bloques `--surface-muted` con pulse; nunca una pantalla en blanco. */
function Skeleton({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      aria-hidden="true"
      className={cn('animate-pulse rounded bg-surface-muted', className)}
      {...props}
    />
  )
}

export { Skeleton }
