import { cn } from '@/lib/utils'

interface ProgressProps {
  /** De 0 a 100; `null` = indeterminado (procesando sin porcentaje). */
  value: number | null
  label: string
  className?: string
}

/** Barra de progreso: pista `--surface-muted` y avance `--primary`. */
export function Progress({ value, label, className }: ProgressProps) {
  const clamped = value === null ? null : Math.min(Math.max(value, 0), 100)
  return (
    <div
      role="progressbar"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={clamped ?? undefined}
      className={cn('h-2 w-full overflow-hidden rounded-full bg-surface-muted', className)}
    >
      <div
        className={cn(
          'h-full rounded-full bg-primary transition-all duration-200 ease-out',
          clamped === null && 'w-full animate-pulse',
        )}
        style={clamped === null ? undefined : { width: `${clamped}%` }}
      />
    </div>
  )
}
