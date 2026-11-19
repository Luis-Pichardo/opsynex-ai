import { Menu } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { ThemeToggle } from './ThemeToggle'
import { UserMenu } from './UserMenu'
import { WorkspaceSelectors } from './WorkspaceSelectors'

interface TopbarProps {
  title: string
  onOpenMenu: () => void
}

/**
 * Topbar (GUIA_DE_DISENO.md §5): 64 px sobre `--surface` con borde inferior. En pantallas
 * pequeñas los selectores de empresa y año pasan a una segunda fila.
 */
export function Topbar({ title, onOpenMenu }: TopbarProps) {
  return (
    <header className="sticky top-0 z-40 border-b border-border bg-surface">
      <div className="flex h-topbar items-center gap-2 px-4 lg:px-8">
        <Button
          variant="ghost"
          size="icon"
          className="-ml-2 lg:hidden"
          onClick={onOpenMenu}
          aria-label="Abrir menú"
        >
          <Menu />
        </Button>
        <p className="min-w-0 flex-1 truncate text-h3 text-foreground">{title}</p>
        <WorkspaceSelectors className="hidden md:flex" />
        <ThemeToggle />
        <UserMenu />
      </div>
      <WorkspaceSelectors className="border-t border-border px-4 py-2 md:hidden" />
    </header>
  )
}
