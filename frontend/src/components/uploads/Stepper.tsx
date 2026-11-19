import { Check } from 'lucide-react'

import { cn } from '@/lib/utils'

interface StepperProps {
  steps: string[]
  /** Paso actual (desde 1). */
  current: number
  /** Pasos con error (se marcan en rojo con su número). */
  failedStep?: number
}

/** Indicador de progreso del asistente: completado (check), actual y pendiente. */
export function Stepper({ steps, current, failedStep }: StepperProps) {
  return (
    <ol className="flex w-full items-start gap-2" aria-label="Pasos de la carga">
      {steps.map((label, index) => {
        const number = index + 1
        const done = number < current
        const active = number === current
        const failed = number === failedStep
        return (
          <li
            key={label}
            className="flex flex-1 flex-col gap-2"
            aria-current={active ? 'step' : undefined}
          >
            <div
              className={cn(
                'h-1 rounded-full transition-colors duration-200 ease-out',
                failed ? 'bg-critical' : done || active ? 'bg-primary' : 'bg-surface-muted',
              )}
            />
            <div className="flex items-center gap-2">
              <span
                className={cn(
                  'inline-flex size-6 shrink-0 items-center justify-center rounded-full text-caption tabular-nums',
                  failed && 'bg-critical-soft text-critical-text',
                  !failed && done && 'bg-primary text-primary-foreground',
                  !failed && active && 'bg-primary-soft text-primary-text',
                  !failed && !done && !active && 'bg-surface-muted text-foreground-secondary',
                )}
              >
                {done && !failed ? <Check className="size-3.5" aria-hidden="true" /> : number}
              </span>
              <span
                className={cn(
                  'text-small',
                  active ? 'font-medium text-foreground' : 'text-foreground-secondary',
                  !active && 'hidden sm:inline',
                )}
              >
                {label}
                <span className="sr-only">
                  {done ? ' (completado)' : active ? ' (actual)' : ''}
                </span>
              </span>
            </div>
          </li>
        )
      })}
    </ol>
  )
}
