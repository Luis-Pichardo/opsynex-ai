import { PanelLeftClose, PanelLeftOpen } from 'lucide-react'
import { NavLink } from 'react-router'

import { useAuth } from '@/auth/useAuth'
import { SimpleTooltip } from '@/components/ui/tooltip'
import { useHasIndustrialData } from '@/industrial/useIndustrial'
import { cn } from '@/lib/utils'
import { Logo } from './Logo'
import { navItemsFor, type NavItem } from './navigation'

interface SidebarProps {
  collapsed?: boolean
  onToggleCollapsed?: () => void
  /** Se llama al elegir un ítem (en móvil cierra el drawer). */
  onNavigate?: () => void
  className?: string
}

const itemBase =
  'group relative flex h-11 items-center gap-3 rounded px-3 text-body font-medium transition-colors duration-150 ease-out focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-muted [&_svg]:size-5 [&_svg]:shrink-0'

/**
 * Sidebar (GUIA_DE_DISENO.md §5): 256 px (72 px colapsada), fondo `--sidebar` en ambos modos.
 * Ítem activo: fondo `--sidebar-active`, barra izquierda de 3 px `--primary` y texto blanco.
 */
export function Sidebar({
  collapsed = false,
  onToggleCollapsed,
  onNavigate,
  className,
}: SidebarProps) {
  const { user } = useAuth()
  const { hasData } = useHasIndustrialData()
  const items = navItemsFor(user?.role, hasData)

  return (
    <nav
      aria-label="Navegación principal"
      className={cn(
        'flex h-full flex-col bg-sidebar text-sidebar-foreground transition-[width] duration-200 ease-out',
        collapsed ? 'w-sidebar-collapsed' : 'w-sidebar',
        className,
      )}
    >
      <div
        className={cn('flex h-topbar shrink-0 items-center', collapsed ? 'justify-center' : 'px-5')}
      >
        <Logo collapsed={collapsed} />
      </div>
      <ul className="flex flex-1 flex-col gap-1 overflow-y-auto px-3 py-4">
        {items.map((item) => (
          <li key={item.to}>
            <SidebarItem item={item} collapsed={collapsed} onNavigate={onNavigate} />
          </li>
        ))}
      </ul>
      {onToggleCollapsed ? (
        <div className="border-t border-sidebar-active p-3">
          <button
            type="button"
            onClick={onToggleCollapsed}
            className={cn(
              itemBase,
              'w-full text-sidebar-muted hover:bg-sidebar-active hover:text-sidebar-foreground',
              collapsed && 'justify-center px-0',
            )}
            aria-label={collapsed ? 'Expandir menú' : 'Colapsar menú'}
          >
            {collapsed ? (
              <PanelLeftOpen aria-hidden="true" />
            ) : (
              <PanelLeftClose aria-hidden="true" />
            )}
            {collapsed ? null : <span>Colapsar menú</span>}
          </button>
        </div>
      ) : null}
    </nav>
  )
}

function SidebarItem({
  item,
  collapsed,
  onNavigate,
}: {
  item: NavItem
  collapsed: boolean
  onNavigate?: () => void
}) {
  const Icon = item.icon
  const content = (
    <>
      <Icon aria-hidden="true" />
      {collapsed ? (
        <span className="sr-only">{item.label}</span>
      ) : (
        <span className="truncate">{item.label}</span>
      )}
    </>
  )

  if (item.disabledReason) {
    return (
      <SimpleTooltip content={item.disabledReason}>
        <span
          role="link"
          aria-disabled="true"
          tabIndex={0}
          className={cn(
            itemBase,
            'cursor-not-allowed text-sidebar-muted opacity-50',
            collapsed && 'justify-center px-0',
          )}
        >
          {content}
        </span>
      </SimpleTooltip>
    )
  }

  const link = (
    <NavLink
      to={item.to}
      end={item.to === '/'}
      onClick={onNavigate}
      className={({ isActive }) =>
        cn(
          itemBase,
          collapsed && 'justify-center px-0',
          isActive
            ? 'bg-sidebar-active text-primary-foreground before:absolute before:inset-y-2 before:left-0 before:w-[3px] before:rounded-full before:bg-primary'
            : 'text-sidebar-muted hover:bg-sidebar-active hover:text-sidebar-foreground',
        )
      }
    >
      {content}
    </NavLink>
  )

  return collapsed ? <SimpleTooltip content={item.label}>{link}</SimpleTooltip> : link
}
