import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { TooltipProvider } from '@/components/ui/tooltip'
import { CATEGORIES, makeAccount } from '@/test/fixtures'
import type { CategoryOption } from '@/types/uploads'
import { MappingReviewTable, type MappingReviewTableProps } from './MappingReviewTable'

const accounts = [
  makeAccount(),
  makeAccount({
    mappingId: 'm-2',
    originalLabel: 'Ventas netas',
    category: 'Revenue',
    confidence: 0.95,
    method: 'Exact',
    isDoubtful: false,
    amount: 185_450_000,
  }),
  makeAccount({
    mappingId: 'm-3',
    originalLabel: 'Caja chica',
    category: 'Cash',
    confidence: 0.6,
    method: 'Fuzzy',
    section: 'Activos corrientes',
  }),
]

function renderTable(props: Partial<MappingReviewTableProps> = {}) {
  const onChange = vi.fn()
  render(
    <TooltipProvider>
      <MappingReviewTable
        accounts={accounts}
        categories={CATEGORIES as unknown as CategoryOption[]}
        currency="DOP"
        edits={{}}
        onChange={onChange}
        {...props}
      />
    </TooltipProvider>,
  )
  return onChange
}

describe('MappingReviewTable', () => {
  it('muestra la cuenta con su hoja, fila y sección, la confianza con texto y el método', () => {
    renderTable()
    const rows = screen.getAllByRole('row')
    const first = within(rows[1]!)
    expect(first.getByText('Partida especial ZQX')).toBeInTheDocument()
    expect(first.getByText(/g&P · fila 26/)).toBeInTheDocument()
    expect(first.getByText('Baja')).toBeInTheDocument()
    expect(first.getByText('Sin clasificar')).toBeInTheDocument()
    expect(within(rows[2]!).getByText('Alta')).toBeInTheDocument()
    expect(within(rows[2]!).getByText('Automático')).toBeInTheDocument()
    expect(within(rows[3]!).getByText('Media')).toBeInTheDocument()
    expect(within(rows[3]!).getByText(/Activos corrientes/)).toBeInTheDocument()
  })

  it('permite cambiar la categoría', async () => {
    const onChange = renderTable()
    await userEvent.click(
      screen.getByRole('combobox', { name: 'Categoría de Partida especial ZQX' }),
    )
    await userEvent.click(await screen.findByRole('option', { name: 'Gastos operacionales' }))
    expect(onChange).toHaveBeenCalledWith('m-1', 'OperatingExpenses')
  })

  it('marca las cuentas con cambios sin guardar', () => {
    renderTable({ edits: { 'm-1': 'OperatingExpenses' } })
    expect(
      screen.getByRole('combobox', { name: 'Categoría de Partida especial ZQX' }),
    ).toHaveTextContent('Gastos operacionales')
    expect(screen.getByLabelText('Por confirmar')).toBeInTheDocument()
  })

  it('las cuentas confirmadas muestran "Confirmada"', () => {
    render(
      <TooltipProvider>
        <MappingReviewTable
          accounts={[
            makeAccount({ isConfirmed: true, method: 'Manual', confidence: 1, isDoubtful: false }),
          ]}
          categories={CATEGORIES as unknown as CategoryOption[]}
          currency="DOP"
          edits={{}}
          onChange={() => {}}
        />
      </TooltipProvider>,
    )
    expect(screen.getAllByText('Confirmada').length).toBeGreaterThan(0)
    expect(screen.getByText('Confirmado')).toBeInTheDocument()
  })
})
