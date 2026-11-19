import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { json, layoutHandlers, makeUser, mockApi, renderApp } from '@/test/utils'

describe('LoginPage', () => {
  it('valida los campos antes de llamar al API', async () => {
    const fetchMock = mockApi({})
    renderApp('/login')
    await userEvent.click(screen.getByRole('button', { name: 'Iniciar sesión' }))
    expect(await screen.findByText('Escribe tu correo.')).toBeInTheDocument()
    expect(screen.getByText('Escribe tu contraseña.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('muestra "Correo o contraseña incorrectos" ante un 401', async () => {
    mockApi({
      'POST /api/v1/auth/login': () => json({ code: 'auth.invalid_credentials', status: 401 }, 401),
    })
    renderApp('/login')
    await userEvent.type(screen.getByLabelText('Correo'), 'ana@demo.test')
    await userEvent.type(screen.getByLabelText('Contraseña'), 'mala1234')
    await userEvent.click(screen.getByRole('button', { name: 'Iniciar sesión' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Correo o contraseña incorrectos.')
  })

  it('entra al dashboard después de iniciar sesión', async () => {
    const user = makeUser('Client')
    mockApi({
      ...layoutHandlers,
      'GET /api/v1/auth/me': user,
      'POST /api/v1/auth/login': {
        accessToken: 'jwt',
        tokenType: 'Bearer',
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
        user,
      },
    })
    renderApp('/login')
    await userEvent.type(screen.getByLabelText('Correo'), 'client@demo.test')
    await userEvent.type(screen.getByLabelText('Contraseña'), 'Clave1234')
    await userEvent.click(screen.getByRole('button', { name: 'Iniciar sesión' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
  })

  it('no ofrece registro y explica cómo recuperar la contraseña', async () => {
    mockApi({})
    renderApp('/login')
    expect(screen.queryByText(/regístrate|crear cuenta/i)).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: '¿Olvidaste tu contraseña?' }))
    expect(screen.getByText(/Pide al administrador/)).toBeInTheDocument()
  })
})
