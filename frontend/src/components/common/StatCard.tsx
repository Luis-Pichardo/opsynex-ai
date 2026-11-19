import { CircleHelp } from 'lucide-react'
import type { ComponentProps, ReactNode } from 'react'

import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { SimpleTooltip } from '@/components/ui/tooltip'
import { cn } from '@/lib/utils'
import { TrendIndicator } from './TrendIndicator'

interface StatCardProps {
  /** Etiqueta (`overline`). */
  label: string
  /** Valor ya formateado con `src/lib/format.ts`. */
  value: string
  /** Ayuda del KPI (fórmula), en un tooltip. */
  hint?: string
  trend?: ComponentProps<typeof TrendIndicator>
  /** Contenido opcional bajo el valor (p. ej. un sparkline de 32 px). */
  children?: ReactNode
  loading?: boolean
  className?: string
}

/** StatCard (GUIA_DE_DISENO.md §6): `overline` → valor `kpi` con `tabular-nums` → variación. */
export function StatCard({
  label,
  value,
  hint,
  trend,
  children,
  loading,
  className,
}: StatCardProps) {
  return (
    <Card className={cn('flex flex-col gap-2 p-4 sm:p-5', className)}>
      <div className="flex items-center gap-1">
        <span className="text-overline uppercase text-foreground-secondary">{label}</span>
        {hint ? (
          <SimpleTooltip content={hint}>
            <button
              type="button"
              className="inline-flex size-5 items-center justify-center rounded-sm text-foreground-muted hover:text-foreground"
              aria-label={`Qué es ${label}`}
            >
              <CircleHelp className="size-3.5" aria-hidden="true" />
            </button>
          </SimpleTooltip>
        ) : null}
      </div>
      {loading ? (
        <>
          <Skeleton className="h-8 w-32" />
          <Skeleton className="h-4 w-20" />
        </>
      ) : (
        <>
          <span className="text-kpi tabular-nums text-foreground">{value}</span>
          {trend ? <TrendIndicator {...trend} /> : null}
          {children}
        </>
      )}
    </Card>
  )
}
