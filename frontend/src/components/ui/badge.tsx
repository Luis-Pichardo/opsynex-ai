import { cva, type VariantProps } from 'class-variance-authority'
import { CircleCheck, Eye, Info, OctagonAlert, TriangleAlert, type LucideIcon } from 'lucide-react'
import type * as React from 'react'

import { cn } from '@/lib/utils'

/**
 * Badge (GUIA_DE_DISENO.md §6): 22 px de alto, radio 6 px, texto `caption`,
 * fondo suave + texto oscuro del mismo tono + icono de 12 px. Nunca solo color.
 */
const badgeVariants = cva(
  'inline-flex h-[22px] items-center gap-1 whitespace-nowrap rounded-sm px-2 text-caption transition-colors duration-150 ease-out [&_svg]:size-3 [&_svg]:shrink-0',
  {
    variants: {
      variant: {
        neutral: 'bg-surface-muted text-foreground-secondary',
        outline: 'border border-border text-foreground-secondary',
        good: 'bg-good-soft text-good-text',
        warning: 'bg-warning-soft text-warning-text',
        serious: 'bg-serious-soft text-serious-text',
        critical: 'bg-critical-soft text-critical-text',
        info: 'bg-info-soft text-info-text',
      },
    },
    defaultVariants: {
      variant: 'neutral',
    },
  },
)

/** Icono de cada variante semántica (GUIA_DE_DISENO.md §2.2). */
const BADGE_ICONS: Partial<Record<NonNullable<BadgeProps['variant']>, LucideIcon>> = {
  critical: OctagonAlert,
  serious: TriangleAlert,
  warning: Eye,
  good: CircleCheck,
  info: Info,
}

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>, VariantProps<typeof badgeVariants> {
  /** Las variantes semánticas incluyen su icono; `false` lo oculta (solo si el texto lo explica). */
  icon?: boolean
}

function Badge({ className, variant, icon = true, children, ...props }: BadgeProps) {
  const Icon = variant ? BADGE_ICONS[variant] : undefined
  return (
    <span className={cn(badgeVariants({ variant }), className)} {...props}>
      {icon && Icon ? <Icon aria-hidden="true" /> : null}
      {children}
    </span>
  )
}

export { Badge, badgeVariants }
