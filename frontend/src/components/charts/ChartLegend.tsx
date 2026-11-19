/** Leyenda arriba a la izquierda (2 o más series); el texto va en color de texto, no de la serie. */
export function ChartLegend({ items }: { items: { label: string; color: string }[] }) {
  if (items.length < 2) return null
  return (
    <ul className="flex flex-wrap gap-x-4 gap-y-1" aria-hidden="true">
      {items.map((item) => (
        <li
          key={item.label}
          className="flex items-center gap-1.5 text-caption text-foreground-secondary"
        >
          <span className="size-2.5 rounded-sm" style={{ background: item.color }} />
          {item.label}
        </li>
      ))}
    </ul>
  )
}
