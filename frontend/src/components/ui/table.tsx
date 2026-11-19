import * as React from 'react'

import { cn } from '@/lib/utils'

/**
 * Table (GUIA_DE_DISENO.md §6): cabecera `overline` sobre `--surface-muted`, filas de 48 px,
 * hover `--surface-muted`; números a la derecha con `tabular-nums` (clase `numeric`).
 */
const Table = React.forwardRef<HTMLTableElement, React.HTMLAttributes<HTMLTableElement>>(
  ({ className, ...props }, ref) => {
    const wrapper = React.useRef<HTMLDivElement>(null)
    const overflowing = useHorizontalOverflow(wrapper)
    const label = props['aria-label']
    return (
      // Si la tabla no cabe (p. ej. a 375 px), el contenedor se desplaza y se puede enfocar
      // para moverlo con el teclado (WCAG 2.1.1).
      <div
        ref={wrapper}
        className="relative w-full overflow-x-auto rounded-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        {...(overflowing
          ? {
              tabIndex: 0,
              role: 'region',
              'aria-label': label ? `${label} (desplazable)` : 'Tabla desplazable',
            }
          : {})}
      >
        <table ref={ref} className={cn('w-full caption-bottom text-body', className)} {...props} />
      </div>
    )
  },
)
Table.displayName = 'Table'

/** `true` si el contenido del elemento es más ancho que el elemento (hay desplazamiento horizontal). */
function useHorizontalOverflow(ref: React.RefObject<HTMLDivElement | null>) {
  const [overflowing, setOverflowing] = React.useState(false)
  React.useEffect(() => {
    const element = ref.current
    if (!element) return
    const update = () => setOverflowing(element.scrollWidth > element.clientWidth + 1)
    update()
    if (typeof ResizeObserver === 'undefined') return
    const observer = new ResizeObserver(update)
    observer.observe(element)
    if (element.firstElementChild) observer.observe(element.firstElementChild)
    return () => observer.disconnect()
  }, [ref])
  return overflowing
}

const TableHeader = React.forwardRef<
  HTMLTableSectionElement,
  React.HTMLAttributes<HTMLTableSectionElement>
>(({ className, ...props }, ref) => (
  <thead ref={ref} className={cn('bg-surface-muted', className)} {...props} />
))
TableHeader.displayName = 'TableHeader'

const TableBody = React.forwardRef<
  HTMLTableSectionElement,
  React.HTMLAttributes<HTMLTableSectionElement>
>(({ className, ...props }, ref) => (
  <tbody ref={ref} className={cn('[&_tr:last-child]:border-0', className)} {...props} />
))
TableBody.displayName = 'TableBody'

const TableRow = React.forwardRef<HTMLTableRowElement, React.HTMLAttributes<HTMLTableRowElement>>(
  ({ className, ...props }, ref) => (
    <tr
      ref={ref}
      className={cn(
        'border-b border-border transition-colors duration-150 ease-out hover:bg-surface-muted',
        className,
      )}
      {...props}
    />
  ),
)
TableRow.displayName = 'TableRow'

const TableHead = React.forwardRef<
  HTMLTableCellElement,
  React.ThHTMLAttributes<HTMLTableCellElement>
>(({ className, ...props }, ref) => (
  <th
    ref={ref}
    className={cn(
      'h-10 whitespace-nowrap px-4 text-left align-middle text-overline uppercase text-foreground-secondary [&.numeric]:text-right',
      className,
    )}
    {...props}
  />
))
TableHead.displayName = 'TableHead'

const TableCell = React.forwardRef<
  HTMLTableCellElement,
  React.TdHTMLAttributes<HTMLTableCellElement>
>(({ className, ...props }, ref) => (
  <td
    ref={ref}
    className={cn(
      'h-12 px-4 align-middle text-body [&.numeric]:text-right [&.numeric]:tabular-nums',
      className,
    )}
    {...props}
  />
))
TableCell.displayName = 'TableCell'

const TableCaption = React.forwardRef<
  HTMLTableCaptionElement,
  React.HTMLAttributes<HTMLTableCaptionElement>
>(({ className, ...props }, ref) => (
  <caption
    ref={ref}
    className={cn('mt-4 text-small text-foreground-secondary', className)}
    {...props}
  />
))
TableCaption.displayName = 'TableCaption'

export { Table, TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow }
