import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { makeIndustrialKpis, makeIndustrialScore } from '@/test/industrial-fixtures'
import { layoutHandlers, makeUser, mockApi, renderApp, storeSession } from '@/test/utils'
import type { UserRole } from '@/types/api'

function navLabels(role: UserRole, handlers: Record<string, unknown> = {}, path = '/') {
  const user = makeUser(role)
  mockApi({ ...layoutHandlers, 'GET /api/v1/auth/me': user, ...handlers })
  storeSession(user)
  renderApp(path)
  const nav = screen.getAllByRole('navigation', { name: 'Navegación principal' })[0]!
  return within(nav)
    .getAllByRole('link')
    .map((link) => link.textContent)
}

describe('Sidebar', () => {
  it('un Client solo ve Dashboard, Problemas, KPIs, Producción y Costos', () => {
    const labels = navLabels('Client')
    expect(labels).toEqual(['Dashboard', 'Problemas', 'KPIs', 'Producción', 'Costos'])
  })

  it('un Analyst ve también la carga y la homologación, pero no la administración', () => {
    const labels = navLabels('Analyst')
    expect(labels).toContain('Carga de datos')
    expect(labels).toContain('Homologación')
    expect(labels).not.toContain('Administración')
  })

  it('el Admin ve todo', () => {
    expect(navLabels('Admin')).toContain('Administración')
  })

  it('Producción y Costos aparecen deshabilitados sin datos industriales', () => {
    navLabels('Admin')
    for (const label of ['Producción', 'Costos']) {
      const item = screen.getAllByText(label)[0]!.closest('[aria-disabled]')
      expect(item).toHaveAttribute('aria-disabled', 'true')
    }
  })

  it('Producción y Costos se habilitan cuando la empresa tiene datos industriales', async () => {
    navLabels(
      'Client',
      {
        'GET /api/v1/industrial/kpis': makeIndustrialKpis(),
        'GET /api/v1/industrial/score': makeIndustrialScore(),
      },
      '/production',
    )
    const nav = screen.getAllByRole('navigation', { name: 'Navegación principal' })[0]!
    await waitFor(() =>
      expect(within(nav).getByRole('link', { name: 'Producción' })).toHaveAttribute(
        'href',
        '/production',
      ),
    )
    expect(within(nav).getByRole('link', { name: 'Costos' })).toHaveAttribute('href', '/costs')
    expect(within(nav).queryByText('Producción')?.closest('[aria-disabled]')).toBeNull()
  })
})
