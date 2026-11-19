import { CircleHelp } from 'lucide-react'

import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { SimpleTooltip } from '@/components/ui/tooltip'
import { formatCurrency } from '@/lib/format'
import type { IndustrialLosses } from '@/types/industrial'

interface LossesCardProps {
  losses: IndustrialLosses | undefined
  currency: string
  /** Período del cálculo ("todo 2024", "nov 2024"). */
  period: string
  loading?: boolean
}

/**
 * Pérdidas valoradas (PLATAFORMA_DE_ANALISIS §7): paradas × costo por minuto + scrap × costo
 * unitario. La pérdida por velocidad se muestra aparte (no entra en la fórmula de la especificación).
 */
export function LossesCard({ losses, currency, period, loading }: LossesCardProps) {
  if (loading) return <Skeleton className="h-80" />
  const money = (v: number | null | undefined) => formatCurrency(v, currency)
  const items = [
    {
      label: 'Paradas',
      value: losses?.downtimeCost,
      hint: 'Minutos de parada × costo por minuto de la máquina.',
    },
    {
      label: 'Scrap',
      value: losses?.scrapCost,
      hint: '(Unidades totales − unidades buenas) × costo unitario.',
    },
  ]
  return (
    <Card className="flex h-full flex-col gap-5 p-4 sm:p-5">
      <div className="space-y-0.5">
        <h2 className="text-h3 text-foreground">Pérdidas operativas</h2>
        <p className="text-small text-foreground-secondary">
          Valor estimado de las paradas y del scrap, {period}.
        </p>
      </div>
      <div className="space-y-1">
        <p className="text-overline uppercase text-foreground-secondary">Impacto total</p>
        <p className="text-kpi tabular-nums text-foreground">{money(losses?.total)}</p>
        <p className="text-caption text-foreground-muted">Paradas + scrap</p>
      </div>
      <dl className="grid gap-4 sm:grid-cols-3">
        {items.map((item) => (
          <div key={item.label} className="space-y-1">
            <dt className="flex items-center gap-1 text-small text-foreground-secondary">
              {item.label}
              <SimpleTooltip content={item.hint}>
                <button
                  type="button"
                  className="text-foreground-muted"
                  aria-label={`Cómo se calcula ${item.label}`}
                >
                  <CircleHelp className="size-3.5" aria-hidden="true" />
                </button>
              </SimpleTooltip>
            </dt>
            <dd className="text-h3 tabular-nums text-foreground">{money(item.value)}</dd>
          </div>
        ))}
        <div className="space-y-1">
          <dt className="flex items-center gap-1 text-small text-foreground-secondary">
            Velocidad (aparte)
            <SimpleTooltip content="(Tiempo en marcha − tiempo al ciclo estándar) × costo por minuto. No entra en el total de la especificación.">
              <button
                type="button"
                className="text-foreground-muted"
                aria-label="Cómo se calcula la pérdida por velocidad"
              >
                <CircleHelp className="size-3.5" aria-hidden="true" />
              </button>
            </SimpleTooltip>
          </dt>
          <dd className="text-h3 tabular-nums text-foreground-secondary">
            {money(losses?.speedLossCost)}
          </dd>
        </div>
      </dl>
    </Card>
  )
}
