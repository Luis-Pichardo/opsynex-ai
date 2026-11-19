import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { makeImpact, makeKpis, makeProblemPage, makeScore } from '@/test/fixtures'
import { layoutHandlers, makeUser, mockApi, renderApp, storeSession } from '@/test/utils'

const n = (s: string | null) => (s ?? '').replace(/\s/gu, ' ')

function setup(overrides: Record<string, unknown> = {}, role: 'Client' | 'Analyst' = 'Client') {
  const user = makeUser(role)
  storeSession(user)
  return mockApi({
    ...layoutHandlers,
    'GET /api/v1/auth/me': user,
    'GET /api/v1/kpis': makeKpis(),
    'GET /api/v1/score': makeScore(),
    'GET /api/v1/impact/summary': makeImpact(),
    'GET /api/v1/problems': makeProblemPage(),
    'GET /api/v1/data': {
      companyId: 'c',
      type: 'Financial',
      year: null,
      availableYears: [2023, 2024],
      financial: [
        { year: 2023, revenue: 169_198_600, totalCost: 142_465_221 },
        { year: 2024, revenue: 181_741_000, totalCost: 155_079_610 },
      ],
    },
    ...overrides,
  })
}

describe('DashboardPage', () => {
  it('muestra score, impacto, KPIs, tendencias, problemas y recomendaciones en el orden de la guía', async () => {
    setup()
    renderApp('/')

    expect(
      await screen.findByRole('img', { name: /Score Opsynex 78 de 100: Oportunidades de mejora/ }),
    ).toBeInTheDocument()
    expect(screen.getByText('Rentabilidad')).toBeInTheDocument()
    expect(screen.getByText(/2,3 pts/)).toBeInTheDocument()

    expect(n((await screen.findByText(/4\.397\.941,56/)).textContent)).toBe('RD$ 4.397.941,56')
    expect(screen.getByText(/2,4 % de los ingresos/)).toBeInTheDocument()

    const kpis = screen.getByRole('region', { name: 'KPIs principales' })
    expect(within(kpis).getByText('18,6 %')).toBeInTheDocument()
    expect(within(kpis).getByText(/baja, desfavorable/)).toBeInTheDocument()
    expect(within(kpis).getByText(/sube, desfavorable/)).toBeInTheDocument() // eficiencia de costos sube = malo

    expect(screen.getByRole('heading', { name: 'Tendencia del KPI' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Ingresos vs. costo total' })).toBeInTheDocument()

    const problems = await screen.findByRole('region', { name: 'Problemas y recomendaciones' })
    expect(
      within(problems).getByRole('link', { name: /Deterioro de rentabilidad/ }),
    ).toHaveAttribute('href', '/problems/p-1')
    expect(within(problems).getAllByText('Baja').length).toBeGreaterThan(0)
    expect(
      within(problems).getAllByText(/revisa qué costos crecieron más que los ingresos/).length,
    ).toBeGreaterThan(0)
    expect(within(problems).getByText('Lo que mejoró')).toBeInTheDocument()

    expect(screen.getByRole('button', { name: 'Descargar informe PDF' })).toBeEnabled()
  })

  it('sin datos invita a subir los estados financieros', async () => {
    setup({ 'GET /api/v1/kpis': { ...makeKpis(), years: [] } }, 'Analyst')
    renderApp('/')
    expect(await screen.findByText('Sube tus estados financieros para empezar')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Subir archivo' })).toHaveAttribute('href', '/uploads')
  })
})
