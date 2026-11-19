interface TooltipEntry {
  name?: string | number
  value?: unknown
  color?: string
  dataKey?: unknown
}

interface ChartTooltipProps {
  active?: boolean
  payload?: readonly TooltipEntry[]
  label?: string | number

  /** Formato de cada valor (siempre con src/lib/format.ts). */
  formatValue: (value: number, name: string) => string
  /** Formato de la etiqueta del eje X (por defecto, tal cual). */
  formatLabel?: (label: string | number) => string
}

/** Tooltip con tokens: superficie, borde y `--shadow-md`; el color de la serie solo en el marcador. */
export function ChartTooltip({
  active,
  payload,
  label,
  formatValue,
  formatLabel,
}: ChartTooltipProps) {
  if (!active || !payload || payload.length === 0) return null
  return (
    <div className="min-w-36 rounded border border-border bg-popover px-3 py-2 text-small text-popover-foreground shadow-md">
      {label !== undefined && label !== '' ? (
        <p className="mb-1 font-medium text-foreground">
          {formatLabel ? formatLabel(label) : String(label)}
        </p>
      ) : null}
      <ul className="space-y-0.5">
        {payload.map((entry) => (
          <li
            key={String(entry.dataKey ?? entry.name)}
            className="flex items-center justify-between gap-4"
          >
            <span className="flex items-center gap-1.5 text-foreground-secondary">
              <span
                className="size-2 rounded-full"
                style={{ background: entry.color }}
                aria-hidden="true"
              />
              {entry.name}
            </span>
            <span className="font-medium tabular-nums text-foreground">
              {typeof entry.value === 'number' ? formatValue(entry.value, String(entry.name)) : '—'}
            </span>
          </li>
        ))}
      </ul>
    </div>
  )
}
