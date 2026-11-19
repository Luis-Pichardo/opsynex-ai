import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'

import { layoutHandlers, makeUser, mockApi, Providers, renderApp, storeSession } from '@/test/utils'
import type { UserRole } from '@/types/api'
import { RoleGuard } from './RoleGuard'

function renderGuard(role: UserRole, roles: UserRole[]) {
  mockApi({ ...layoutHandlers, 'GET /api/v1/auth/me': makeUser(role) })
  storeSession(makeUser(role))
  return render(
    <Providers>
      <MemoryRouter>
        <RoleGuard roles={roles}>
          <p>Contenido protegido</p>
        </RoleGuard>
      </MemoryRouter>
    </Providers>,
  )
}

describe('RoleGuard', () => {
  it('muestra el contenido a los roles permitidos', () => {
    renderGuard('Analyst', ['Admin', 'Analyst'])
    expect(screen.getByText('Contenido protegido')).toBeInTheDocument()
  })

  it('bloquea a los demás roles', () => {
    renderGuard('Client', ['Admin', 'Analyst'])
    expect(screen.queryByText('Contenido protegido')).not.toBeInTheDocument()
    expect(screen.getByText('No tienes acceso a esta sección')).toBeInTheDocument()
  })
})

describe('Rutas protegidas', () => {
  function asRole(role: UserRole, path: string) {
    const user = makeUser(role)
    mockApi({ ...layoutHandlers, 'GET /api/v1/auth/me': user, 'GET /api/v1/users': [] })
    storeSession(user)
    return renderApp(path)
  }

  it.each(['/uploads', '/mappings', '/admin/companies', '/admin/users'])(
    'un Client no puede entrar a %s por URL directa',
    async (path) => {
      asRole('Client', path)
      expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
    },
  )

  it('un Analyst no puede entrar a Administración', async () => {
    asRole('Analyst', '/admin/users')
    expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
  })

  it('un Analyst sí entra a Carga de datos', async () => {
    asRole('Analyst', '/uploads')
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Carga de datos' }),
    ).toBeInTheDocument()
  })

  it('el Admin entra a Administración', async () => {
    asRole('Admin', '/admin')
    expect(await screen.findByRole('heading', { level: 2, name: 'Empresas' })).toBeInTheDocument()
  })

  it('sin sesión lleva al login', async () => {
    mockApi({})
    renderApp('/kpis')
    expect(await screen.findByRole('heading', { name: 'Inicia sesión' })).toBeInTheDocument()
  })
})
