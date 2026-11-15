import { screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { ApiHealthCard } from '@/components/health/ApiHealthCard'
import { renderWithQuery } from '@/test/utils'

const healthy = {
  status: 'Healthy',
  service: 'opsynex-api',
  version: '0.1.0',
  totalDurationMs: 1.2,
  checks: [{ name: 'self', status: 'Healthy', durationMs: 0.1 }],
}

describe('ApiHealthCard', () => {
  it('muestra "En línea" cuando el API responde Healthy', async () => {
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(JSON.stringify(healthy), { status: 200 }))

    renderWithQuery(<ApiHealthCard />)

    expect(screen.getByRole('status')).toHaveTextContent('Comprobando…')
    expect(await screen.findByText('En línea')).toBeInTheDocument()
    expect(screen.getByText('0.1.0')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(expect.stringMatching(/\/health$/), expect.anything())
  })

  it('muestra "Con fallas" cuando el API responde 503 Unhealthy', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ ...healthy, status: 'Unhealthy' }), { status: 503 }),
    )

    renderWithQuery(<ApiHealthCard />)

    expect(await screen.findByText('Con fallas')).toBeInTheDocument()
  })

  it('muestra "Sin conexión" y un mensaje de ayuda si el API no responde', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new TypeError('Failed to fetch'))

    renderWithQuery(<ApiHealthCard />)

    expect(await screen.findByText('Sin conexión')).toBeInTheDocument()
    expect(screen.getByText(/No se pudo conectar con el API/)).toBeInTheDocument()
  })
})
