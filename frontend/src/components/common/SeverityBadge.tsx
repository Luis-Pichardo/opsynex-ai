import { Badge } from '@/components/ui/badge'

export type Severity = 'High' | 'Medium' | 'Low'

const SEVERITY = {
  High: { variant: 'critical', label: 'Alta' },
  Medium: { variant: 'serious', label: 'Media' },
  Low: { variant: 'warning', label: 'Baja' },
} as const

/** Severidad: fondo suave + texto + icono + palabra (GUIA_DE_DISENO.md §2.2). */
export function SeverityBadge({ severity }: { severity: Severity }) {
  const { variant, label } = SEVERITY[severity]
  return (
    <Badge variant={variant} aria-label={`Severidad ${label.toLowerCase()}`}>
      {label}
    </Badge>
  )
}
