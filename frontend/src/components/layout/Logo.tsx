import { cn } from '@/lib/utils'

/** Marca de Opsynex AI: isotipo (color de marca) y nombre. */
export function Logo({
  collapsed = false,
  className,
}: {
  collapsed?: boolean
  className?: string
}) {
  return (
    <span className={cn('inline-flex items-center gap-2', className)}>
      <svg viewBox="0 0 32 32" className="size-8 shrink-0" aria-hidden="true">
        <rect width="32" height="32" rx="8" className="fill-primary" />
        <path
          d="M16 7a9 9 0 1 0 9 9"
          fill="none"
          strokeWidth="3"
          strokeLinecap="round"
          className="stroke-primary-foreground"
        />
        <circle cx="23.5" cy="8.5" r="2.5" className="fill-primary-foreground" />
      </svg>
      {collapsed ? (
        <span className="sr-only">Opsynex AI</span>
      ) : (
        <span className="text-h3 font-semibold tracking-tight">Opsynex AI</span>
      )}
    </span>
  )
}
