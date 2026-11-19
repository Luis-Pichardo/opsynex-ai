import type { ReactNode } from 'react'

interface PageHeaderProps {
  title: string
  description?: ReactNode
  /** Acciones a la derecha (un solo botón primario por vista). */
  actions?: ReactNode
}

/** Encabezado de página: título `h1` (uno por página), descripción y acciones. */
export function PageHeader({ title, description, actions }: PageHeaderProps) {
  return (
    <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
      <div className="space-y-1">
        <h1 className="text-h1 text-foreground">{title}</h1>
        {description ? (
          <p className="max-w-prose text-body text-foreground-secondary">{description}</p>
        ) : null}
      </div>
      {actions ? <div className="flex flex-wrap gap-2">{actions}</div> : null}
    </header>
  )
}
