import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { makeKpis, makeProblemDetail, makeProblemPage } from '@/test/fixtures'
import { json, layoutHandlers, makeUser, mockApi, renderApp, storeSession } from '@/test/utils'

function setup(role: 'Client' | 'Analyst') {
  const user = makeUser(role)
  storeSession(user)
  const patched: unknown[] = []
  const fetchMock = mockApi({
    ...layoutHandlers,
    'GET /api/v1/auth/me': user,
    'GET /api/v1/kpis': makeKpis(),
    'GET /api/v1/problems': makeProblemPage(),
    'GET /api/v1/problems/p-1': makeProblemDetail(),
    'PATCH /api/v1/problems/p-1': ({ init }: { init: RequestInit }) => {
      patched.push(JSON.parse(String(init.body)))
      return makeProblemDetail({ status: 'Acknowledged' })
    },
    'GET /api/v1/reports/executive': () =>
      new Response(new Blob(['%PDF-1.7']), {
        status: 200,
        headers: {
          'Content-Type': 'application/pdf',
          'Content-Disposition': "attachment; filename*=UTF-8''informe_empresa_demo_2024.pdf",
        },
      }),
  })
  return { fetchMock, patched }
}

describe('ProblemsPage', () => {
  it('lista los problemas con severidad, impacto y estado, y filtra por severidad', async () => {
    const { fetchMock } = setup('Client')
    renderApp('/problems')
    const row = (await screen.findByRole('link', { name: 'Deterioro de rentabilidad' })).closest(
      'tr',
    )!
    expect(within(row).getByText('Baja')).toBeInTheDocument()
    expect(within(row).getByText(/RD\$ 2\.071\.847,40/)).toBeInTheDocument()
    expect(within(row).getByText('Abierto')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('combobox', { name: 'Severidad' }))
    await userEvent.click(await screen.findByRole('option', { name: 'Alta' }))
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([url]) => String(url).includes('severity=High') && String(url).includes('year=2024'),
        ),
      ).toBe(true),
    )
  })
})

describe('ProblemDetailPage', () => {
  it('muestra impacto con su cálculo, histórico, evidencia y recomendación; el Analyst cambia el estado', async () => {
    const { patched } = setup('Analyst')
    renderApp('/problems/p-1')

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Deterioro de rentabilidad' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Cómo se calculó')).toBeInTheDocument()
    expect(
      screen.getByText('(margen anterior − margen actual) × ingresos actuales'),
    ).toBeInTheDocument()
    expect(screen.getByText('Margen anterior')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /Histórico/ })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Evidencia' })).toBeInTheDocument()
    expect(screen.getByText(/revisa qué costos crecieron/)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('combobox', { name: 'Estado' }))
    await userEvent.click(await screen.findByRole('option', { name: 'Reconocido' }))
    await waitFor(() => expect(patched).toEqual([{ status: 'Acknowledged' }]))
  })

  it('el Client ve el detalle sin poder cambiar el estado y puede descargar el PDF', async () => {
    const { fetchMock } = setup('Client')
    URL.createObjectURL = () => 'blob:informe'
    URL.revokeObjectURL = () => {}
    renderApp('/problems/p-1')
    await screen.findByRole('heading', { level: 1, name: 'Deterioro de rentabilidad' })
    expect(screen.queryByRole('combobox', { name: 'Estado' })).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Descargar informe PDF' }))
    await waitFor(() => {
      const call = fetchMock.mock.calls.find(([url]) => String(url).includes('/reports/executive'))
      expect(call).toBeDefined()
      expect(String(call![0])).toContain('year=2024')
      expect((call![1]!.headers as Record<string, string>).Authorization).toBe(
        'Bearer token-Client',
      )
    })
  })

  it('un problema inexistente muestra un estado vacío', async () => {
    setup('Client')
    mockApi({
      ...layoutHandlers,
      'GET /api/v1/auth/me': makeUser('Client'),
      'GET /api/v1/kpis': makeKpis(),
      'GET /api/v1/problems/zzz': () => json({ code: 'not_found', status: 404 }, 404),
    })
    renderApp('/problems/zzz')
    expect(await screen.findByText('No encontramos este problema')).toBeInTheDocument()
  })
})
