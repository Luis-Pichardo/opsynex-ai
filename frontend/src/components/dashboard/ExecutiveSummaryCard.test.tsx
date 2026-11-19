import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'

import { layoutHandlers, makeUser, mockApi, Providers, storeSession } from '@/test/utils'
import type { AiSummary } from '@/types/analysis'
import { ExecutiveSummaryCard } from './ExecutiveSummaryCard'

const READY: AiSummary = {
  companyId: 'c',
  year: 2024,
  status: 'ready',
  paragraphs: [
    'La empresa tiene un score de 78.',
    'El margen EBITDA bajó 1,14 pp.',
    'Mejoró la liquidez.',
  ],
  priorities: ['Recuperar el margen', 'Controlar la nómina', 'Revisar gastos generales'],
  model: 'claude-opus-5-5',
  generatedAt: '2026-11-19T10:00:00Z',
  message: null,
}

function setup(role: 'Analyst' | 'Client', summary: AiSummary, regenerate?: () => unknown) {
  const user = makeUser(role)
  storeSession(user)
  const fetchMock = mockApi({
    ...layoutHandlers,
    'GET /api/v1/auth/me': user,
    'GET /api/v1/ai/summary': summary,
    ...(regenerate ? { 'POST /api/v1/ai/regenerate': regenerate } : {}),
  })
  render(
    <Providers>
      <MemoryRouter>
        <ExecutiveSummaryCard companyId="c" year={2024} />
      </MemoryRouter>
    </Providers>,
  )
  return fetchMock
}

describe('ExecutiveSummaryCard', () => {
  it('muestra el resumen con la etiqueta "Generado con IA", la fecha y las 3 prioridades', async () => {
    setup('Analyst', READY)
    expect(await screen.findByText('La empresa tiene un score de 78.')).toBeInTheDocument()
    expect(screen.getAllByText('Generado con IA').length).toBeGreaterThan(0)
    expect(screen.getByText(/generado el 19 nov 2026/)).toBeInTheDocument()
    expect(screen.getAllByRole('listitem').map((li) => li.textContent)).toEqual([
      '1Recuperar el margen',
      '2Controlar la nómina',
      '3Revisar gastos generales',
    ])
    expect(screen.getByRole('button', { name: 'Regenerar' })).toBeInTheDocument()
  })

  it('el Client no puede regenerar', async () => {
    setup('Client', READY)
    await screen.findByText('La empresa tiene un score de 78.')
    expect(screen.queryByRole('button', { name: 'Regenerar' })).not.toBeInTheDocument()
  })

  it('al regenerar muestra que se está generando', async () => {
    const fetchMock = setup('Analyst', READY, () => ({
      ...READY,
      status: 'pending',
      message: 'Generando…',
    }))
    await userEvent.click(await screen.findByRole('button', { name: 'Regenerar' }))
    expect(await screen.findByText(/Generando el análisis con IA/)).toBeInTheDocument()
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.some(
          ([url, init]) =>
            String(url).includes('/ai/regenerate?year=2024') && init?.method === 'POST',
        ),
      ).toBe(true),
    )
  })

  it('sin clave de Claude explica que se usan las recomendaciones base', async () => {
    setup('Analyst', {
      ...READY,
      status: 'not_configured',
      paragraphs: [],
      priorities: [],
      generatedAt: null,
      message: 'La IA generativa no está configurada. Las recomendaciones base siguen disponibles.',
    })
    expect(await screen.findByText(/La IA generativa no está configurada/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Regenerar|Generar/ })).not.toBeInTheDocument()
  })

  it('si el servicio de IA falla lo dice y permite reintentar', async () => {
    setup('Analyst', {
      ...READY,
      status: 'unavailable',
      paragraphs: [],
      priorities: [],
      message: 'No se pudo conectar con Claude.',
    })
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'No se pudo conectar con Claude. Las recomendaciones base siguen disponibles.',
    )
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument()
  })
})
