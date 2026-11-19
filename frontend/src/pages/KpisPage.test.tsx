import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { makeKpis } from '@/test/fixtures'
import { layoutHandlers, makeUser, mockApi, renderApp, storeSession } from '@/test/utils'

describe('KpisPage', () => {
  it('muestra la tabla histórica con la variación y un gráfico por KPI con "Ver tabla"', async () => {
    const user = makeUser('Client')
    storeSession(user)
    mockApi({ ...layoutHandlers, 'GET /api/v1/auth/me': user, 'GET /api/v1/kpis': makeKpis() })
    renderApp('/kpis')

    const table = await screen.findByRole('table')
    const ebitda = within(table).getByText('Margen EBITDA').closest('tr')!
    expect(within(ebitda).getByText('19,8 %')).toBeInTheDocument()
    expect(within(ebitda).getByText('18,6 %')).toBeInTheDocument()
    expect(within(ebitda).getByText(/baja, desfavorable/)).toBeInTheDocument()
    const current = within(table).getByText('Razón corriente').closest('tr')!
    expect(within(current).getByText('2,77×')).toBeInTheDocument()
    expect(within(current).getByText(/sube, favorable/)).toBeInTheDocument()

    expect(
      screen.getByRole('img', { name: /Margen EBITDA: 2023 19,8\s%, 2024 18,6\s%/ }),
    ).toBeInTheDocument()
    const buttons = screen.getAllByRole('button', { name: 'Ver tabla' })
    await userEvent.click(buttons[0]!)
    expect(screen.getByRole('button', { name: 'Ver gráfico' })).toBeInTheDocument()
  })
})
