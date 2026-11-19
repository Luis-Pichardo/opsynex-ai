import * as React from 'react'

import { cn } from '@/lib/utils'

/** Input (GUIA_DE_DISENO.md §6): 40 px, borde `--input`, foco con ring de 2 px. */
const Input = React.forwardRef<HTMLInputElement, React.InputHTMLAttributes<HTMLInputElement>>(
  ({ className, type, ...props }, ref) => (
    <input
      ref={ref}
      type={type}
      className={cn(
        'flex h-10 w-full rounded border border-input bg-surface px-3 text-body text-foreground transition-colors duration-150 ease-out placeholder:text-foreground-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background disabled:cursor-not-allowed disabled:bg-surface-muted disabled:opacity-50 aria-[invalid=true]:border-critical',
        className,
      )}
      {...props}
    />
  ),
)
Input.displayName = 'Input'

export { Input }
