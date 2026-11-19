import { Badge } from '@/components/ui/badge'
import { formatPercent } from '@/lib/format'

interface ThresholdStatusProps {
  value: number | null | undefined
  threshold: number
  /** `true` si el valor debe quedar por encima del umbral (OEE); `false` si por debajo (scrap, paradas). */
  higherIsBetter: boolean
}

/**
 * Estado frente al umbral de la regla industrial: badge con icono y palabra (nunca el color solo,
 * GUIA_DE_DISENO.md §2.2). Solo se marca lo que dispara un problema.
 */
export function ThresholdStatus({ value, threshold, higherIsBetter }: ThresholdStatusProps) {
  if (value === null || value === undefined) return null
  const breached = higherIsBetter ? value < threshold : value > threshold
  return breached ? (
    <Badge variant="critical">
      {higherIsBetter ? 'Bajo' : 'Sobre'} el {formatPercent(threshold, 0)}
    </Badge>
  ) : (
    <Badge variant="good">En rango</Badge>
  )
}
