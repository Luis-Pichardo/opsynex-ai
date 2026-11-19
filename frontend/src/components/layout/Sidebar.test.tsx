import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { layoutHandlers, makeUser, mockApi, renderApp, storeSession } from '@/test/utils'
import type { UserRole } from '@/types/api'

function navLabels(role: UserRole) {
  const user = makeUser(role)
  mockApi({ ...layoutHandlers, 'GET /api/v1/auth/me': user })
  storeSession(user)
  renderApp('/')
  const nav = screen.getAllByRole('navigation', { name: 'Navegación principal' })[0]!
  return within(nav)
    .getAllByRole('link')
    .map((link) => link.textContent)
}

describe('Sidebar', () => {
  it('un Client solo ve Dashboard, Problemas, KPIs y Producción', () => {
    const labels = navLabels('Client')
    expect(labels).toEqual(['Dashboard', 'Problemas', 'KPIs', 'Producción'])
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

  it('Producción aparece deshabilitada', () => {
    navLabels('Admin')
    const production = screen.getAllByText('Producción')[0]!.closest('[aria-disabled]')
    expect(production).toHaveAttribute('aria-disabled', 'true')
  })
})
