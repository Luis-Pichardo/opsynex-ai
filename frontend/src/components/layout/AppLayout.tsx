import { useState } from 'react'
import { Outlet, useLocation, useMatches } from 'react-router'

import { Sheet, SheetContent, SheetDescription, SheetTitle } from '@/components/ui/sheet'
import { readStorage, writeStorage } from '@/lib/storage'
import { cn } from '@/lib/utils'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'

const COLLAPSED_KEY = 'opsynex.sidebar.collapsed'

export interface RouteHandle {
  title?: string
}

/** Layout principal: sidebar (drawer bajo `lg`), topbar y contenido de hasta 1440 px. */
export function AppLayout() {
  const [collapsed, setCollapsed] = useState(() => readStorage(COLLAPSED_KEY) === 'true')
  const [mobileOpen, setMobileOpen] = useState(false)
  const location = useLocation()
  const matches = useMatches()
  const title =
    [...matches]
      .reverse()
      .map((m) => (m.handle as RouteHandle | undefined)?.title)
      .find(Boolean) ?? 'Opsynex AI'

  const toggleCollapsed = () => {
    setCollapsed((current) => {
      writeStorage(COLLAPSED_KEY, String(!current))
      return !current
    })
  }

  return (
    <div className="min-h-screen bg-background">
      <a
        href="#contenido"
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:rounded focus:bg-surface focus:px-4 focus:py-2"
      >
        Saltar al contenido
      </a>
      <Sidebar
        collapsed={collapsed}
        onToggleCollapsed={toggleCollapsed}
        className="fixed inset-y-0 left-0 z-30 hidden lg:flex"
      />
      <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
        <SheetContent
          side="left"
          className="p-0 text-sidebar-foreground"
          onOpenAutoFocus={(event) => {
            // Foco en la página actual (no en un ítem deshabilitado con tooltip).
            event.preventDefault()
            const nav = event.currentTarget as HTMLElement
            nav.querySelector<HTMLElement>('a[aria-current="page"], a[href]')?.focus()
          }}
        >
          <SheetTitle className="sr-only">Menú</SheetTitle>
          <SheetDescription className="sr-only">
            Navegación principal de Opsynex AI
          </SheetDescription>
          <Sidebar onNavigate={() => setMobileOpen(false)} />
        </SheetContent>
      </Sheet>
      <div
        className={cn(
          'flex min-h-screen flex-col transition-[padding] duration-200 ease-out',
          collapsed ? 'lg:pl-sidebar-collapsed' : 'lg:pl-sidebar',
        )}
      >
        <Topbar title={title} onOpenMenu={() => setMobileOpen(true)} />
        <main
          id="contenido"
          key={location.pathname}
          className="mx-auto w-full max-w-content flex-1 px-4 py-6 lg:px-8 lg:py-8"
        >
          <Outlet />
        </main>
      </div>
    </div>
  )
}
