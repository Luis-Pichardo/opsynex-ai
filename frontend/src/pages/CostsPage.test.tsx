import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { makeIndustrialKpis, makeIndustrialScore } from '@/test/industrial-fixtures'
import { layoutHandlers, makeUser, mockApi, renderApp, storeSession } from '@/test/utils'

function setup(kpis = makeIndustrialKpis()) {
  const user = makeUser('Client')
  storeSession(user)
  mockApi({
    ...layoutHandlers,
    'GET /api/v1/auth/me': user,
    'GET /api/v1/industrial/kpis': kpis,
    'GET /api/v1/industrial/score': makeIndustrialScore(),
  })
}

describe('CostsPage', () => {
  it('compara el costo por unidad con el estándar y muestra la composición', async () => {
    setup()
    renderApp('/costs')

    const stats = await screen.findByRole('region', { name: 'Indicadores de costos' })
    expect(within(stats).getByText('RD$ 36,20')).toBeInTheDocument()
    expect(within(stats).getByText('RD$ 33,40')).toBeInTheDocument()
    expect(within(stats).getByText('+8,4 % (tolerancia 10 %)')).toBeInTheDocument()

    const table = screen.getByRole('table', { name: 'Costos por máquina' })
    const maq2 = within(table).getByText('MAQ-02').closest('tr')!
    expect(within(maq2).getByText('+17,8 % sobre el estándar')).toBeInTheDocument()
    const maq1 = within(table).getByText('MAQ-01').closest('tr')!
    expect(within(maq1).getByText(/1,9\s%\svs\. estándar/)).toBeInTheDocument()

    expect(
      screen.getByRole('img', {
        name: /Costo por unidad por máquina: MAQ-01 RD\$ 31,87 frente a RD\$ 32,50/,
      }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('img', { name: /Composición del costo por mes: oct 2024/ }),
    ).toBeInTheDocument()
  })

  it('sin costos explica qué plantilla falta', async () => {
    const base = makeIndustrialKpis()
    setup({ ...base, summary: { ...base.summary!, costs: null } })
    renderApp('/costs')

    expect(await screen.findByText('Faltan los costos de producción')).toBeInTheDocument()
  })
})
