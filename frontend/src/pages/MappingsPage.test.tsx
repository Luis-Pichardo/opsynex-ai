import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { CATEGORIES, makeAccount, makeSummary } from '@/test/fixtures'
import { layoutHandlers, makeUser, mockApi, renderApp, storeSession } from '@/test/utils'

const UPLOAD = {
  id: 'u-1',
  companyId: 'c',
  fileName: 'inc_cuentas_desconocidas.xls',
  fileType: 'AccountingWorkbook',
  status: 'Processed',
  years: [2023, 2024],
  rowsCount: 300,
  warningsCount: 2,
  errorsCount: 0,
  createdAt: '2026-11-19T10:00:00Z',
  analysisStatus: 'Completed',
  needsReview: true,
  uploadedBy: 'Ana',
}

function setup() {
  const user = makeUser('Analyst')
  storeSession(user)
  let consolidated = false
  const saved: unknown[] = []
  const fetchMock = mockApi({
    ...layoutHandlers,
    'GET /api/v1/auth/me': user,
    'GET /api/v1/uploads': [UPLOAD],
    'GET /api/v1/mappings/categories': CATEGORIES,
    'GET /api/v1/mappings/pending': () => [
      makeAccount(),
      makeAccount({ mappingId: 'm-2', originalLabel: 'Ajuste extraordinario QWV', amount: 48_500 }),
      makeAccount({
        mappingId: 'm-3',
        originalLabel: 'Ventas netas',
        category: 'Revenue',
        confidence: 0.95,
        method: 'Exact',
        isDoubtful: false,
      }),
    ],
    'PUT /api/v1/mappings': ({ init }: { init: RequestInit }) => {
      saved.push(JSON.parse(String(init.body)))
      return []
    },
    'POST /api/v1/mappings/consolidate': () => {
      consolidated = true
      return {
        uploadId: 'u-1',
        status: 'Processed',
        analysisStatus: 'Completed',
        needsReview: false,
        doubtfulAccounts: 0,
        usedFallback: false,
        years: [2023, 2024],
      }
    },
    'GET /api/v1/analysis/summary': () =>
      consolidated ? makeSummary({ score: 67 }) : makeSummary({ score: 64 }),
  })
  renderApp('/mappings')
  return { fetchMock, saved }
}

describe('MappingsPage', () => {
  it('empieza mostrando solo las cuentas dudosas', async () => {
    setup()
    expect(await screen.findByText('Partida especial ZQX')).toBeInTheDocument()
    expect(screen.getByText('Ajuste extraordinario QWV')).toBeInTheDocument()
    expect(screen.queryByText('Ventas netas')).not.toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'Mostrar' })).toHaveTextContent('Solo dudosas (2)')
    expect(screen.getByRole('button', { name: 'Guardar y volver a analizar' })).toBeDisabled()
  })

  it('confirma, guarda y vuelve a analizar mostrando lo que cambió', async () => {
    const { saved } = setup()
    await screen.findByText('Partida especial ZQX')

    await userEvent.click(screen.getByRole('button', { name: 'Confirmar todas' }))
    expect(screen.getByText('2 cuentas por confirmar.')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Guardar y volver a analizar' }))

    expect(await screen.findByRole('heading', { name: 'Análisis actualizado' })).toBeInTheDocument()
    expect(saved).toEqual([
      {
        items: [
          { id: 'm-1', category: 'Other' },
          { id: 'm-2', category: 'Other' },
        ],
      },
    ])
    const changes = screen.getByText('Qué cambió con el nuevo análisis').parentElement!
    expect(changes).toHaveTextContent('Score64,067,0')
    expect(changes).toHaveTextContent('Cuentas dudosas20')
    expect(screen.getByText('No quedan cuentas dudosas en esta carga.')).toBeInTheDocument()
  })
})
