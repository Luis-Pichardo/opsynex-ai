import { CircleCheck, Info, LoaderCircle, OctagonAlert, TriangleAlert } from 'lucide-react'
import { Toaster as Sonner } from 'sonner'

import { useMediaQuery } from '@/lib/use-media-query'
import { useTheme } from '@/theme/useTheme'

/**
 * Toasts (GUIA_DE_DISENO.md §6): arriba a la derecha (abajo en móvil), icono semántico,
 * 4 s; los errores se crean con `duration: Infinity` para que no se cierren solos.
 */
function Toaster() {
  const { resolvedTheme } = useTheme()
  const isDesktop = useMediaQuery('(min-width: 768px)')
  return (
    <Sonner
      theme={resolvedTheme}
      position={isDesktop ? 'top-right' : 'bottom-center'}
      duration={4000}
      closeButton
      icons={{
        success: <CircleCheck className="size-4 text-good-text" aria-hidden="true" />,
        info: <Info className="size-4 text-info-text" aria-hidden="true" />,
        warning: <TriangleAlert className="size-4 text-serious-text" aria-hidden="true" />,
        error: <OctagonAlert className="size-4 text-critical-text" aria-hidden="true" />,
        loading: <LoaderCircle className="size-4 animate-spin" aria-hidden="true" />,
      }}
      toastOptions={{
        unstyled: true,
        classNames: {
          toast:
            'flex w-full items-start gap-3 rounded border border-border bg-surface p-4 text-foreground shadow-md md:w-96',
          title: 'text-body font-medium',
          description: 'text-small text-foreground-secondary',
          icon: 'mt-1',
          closeButton:
            'order-last ml-auto inline-flex size-6 items-center justify-center rounded-sm text-foreground-secondary hover:bg-surface-muted',
          actionButton:
            'rounded bg-primary px-3 py-1 text-small font-medium text-primary-foreground',
        },
      }}
    />
  )
}

export { Toaster }
