import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import {
  makeIndustrialKpis,
  makeIndustrialScore,
  NO_INDUSTRIAL_DATA,
} from '@/test/industrial-fixtures'
import { layoutHandlers, makeUser, mockApi, renderApp, storeSession } from '@/test/utils'

function setup(kpis = makeIndustrialKpis(), role: 'Client' | 'Analyst' = 'Client') {
  const user = makeUser(role)
  storeSession(user)
  const fetchMock = mockApi({
    ...layoutHandlers,
    'GET /api/v1/auth/me': user,
    'GET /api/v1/industrial/kpis': kpis,
    'GET /api/v1/industrial/score': makeIndustrialScore(),
  })
  return fetchMock
}

describe('ProductionPage', () => {
  it('muestra el OEE con sus 3 componentes, las pérdidas y el score industrial', async () => {
    setup()
    renderApp('/production')

    const stats = await screen.findByRole('region', { name: 'Indicadores de producción' })
    expect(within(stats).getByText('75,1 %')).toBeInTheDocument() // OEE
    expect(within(stats).getByText('87,7 %')).toBeInTheDocument() // disponibilidad
    expect(within(stats).getByText('90,0 %')).toBeInTheDocument() // rendimiento
    expect(within(stats).getByText('95,1 %')).toBeInTheDocument() // calidad
    expect(within(stats).getByText('27.626 min · 78 días')).toBeInTheDocument()
    expect(within(stats).getAllByText('En rango').length).toBeGreaterThan(0)

    expect(screen.getByText('RD$ 1.907.346,00')).toBeInTheDocument() // paradas + scrap
    expect(
      await screen.findByRole('img', { name: /Score industrial 68 de 100/ }),
    ).toBeInTheDocument()
    expect(screen.getByText('Score Opsynex del año: 61')).toBeInTheDocument()
    expect(
      screen.getByRole('img', { name: /Paradas por máquina: MAQ-01 3\.?144 min/ }),
    ).toBeInTheDocument()
  })

  it('ordena el ranking de máquinas por OEE y marca la que está bajo el mínimo', async () => {
    setup()
    renderApp('/production')

    const table = await screen.findByRole('table', { name: 'Ranking de máquinas por OEE' })
    const rows = within(table).getAllByRole('row').slice(1)
    expect(rows.map((r) => within(r).getAllByRole('cell')[1]!.textContent)).toEqual([
      'MAQ-03LINEA-2',
      'MAQ-02LINEA-1',
      'MAQ-01LINEA-1',
    ])
    expect(within(rows[0]!).getByText('Bajo el 60 %')).toBeInTheDocument()
    expect(within(rows[0]!).getByText('56,7 %')).toBeInTheDocument()
    expect(within(rows[2]!).getByText('En rango')).toBeInTheDocument()
  })

  it('filtra por mes y máquina con los parámetros del API y los guarda en la URL', async () => {
    const fetchMock = setup()
    renderApp('/production')

    await userEvent.click(await screen.findByRole('combobox', { name: 'Período' }))
    await userEvent.click(await screen.findByRole('option', { name: 'nov 2024' }))
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([url]) => String(url).includes('/industrial/kpis?month=11')),
      ).toBe(true),
    )

    await userEvent.click(screen.getByRole('combobox', { name: 'Máquina' }))
    await userEvent.click(await screen.findByRole('option', { name: 'MAQ-03' }))
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(([url]) =>
          /industrial\/score\?month=11&machineId=MAQ-03/.test(String(url)),
        ),
      ).toBe(true),
    )
    expect(screen.getByRole('button', { name: 'Quitar filtros' })).toBeInTheDocument()
  })

  it('sin datos industriales invita al Analyst a subir las plantillas', async () => {
    setup(NO_INDUSTRIAL_DATA, 'Analyst')
    renderApp('/production')

    expect(await screen.findByText('Todavía no hay datos de producción')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Subir datos de producción' })).toHaveAttribute(
      'href',
      '/uploads',
    )
  })

  it('con un filtro inválido en la URL ofrece quitar los filtros', async () => {
    const user = makeUser('Client')
    storeSession(user)
    mockApi({
      ...layoutHandlers,
      'GET /api/v1/auth/me': user,
      // Sin filtros (menú) hay datos; con ?year=2023 el API responde 400. Cada llamada recibe
      // su propia Response (el cuerpo solo se puede leer una vez).
      'GET /api/v1/industrial/kpis': ({ url }: { url: URL }) =>
        url.searchParams.has('year')
          ? new Response(
              JSON.stringify({
                title: 'Validación',
                detail: 'No hay datos industriales de 2023.',
                code: 'industrial.no_data_for_year',
                status: 400,
              }),
              { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
            )
          : makeIndustrialKpis(),
    })
    renderApp('/production?year=2023')

    expect(
      await screen.findByText('No hay datos con esos filtros', {}, { timeout: 3000 }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Quitar filtros' })).toBeInTheDocument()
  })
})
