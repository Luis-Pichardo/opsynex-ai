import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { uploadFile } from '@/api/uploads'
import { makeReport, makeSummary } from '@/test/fixtures'
import { layoutHandlers, makeUser, mockApi, Providers, storeSession } from '@/test/utils'
import { UploadWizard } from './UploadWizard'

vi.mock('@/api/uploads', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/uploads')>()),
  uploadFile: vi.fn(),
}))

const mockedUpload = vi.mocked(uploadFile)

function renderWizard() {
  const user = makeUser('Analyst')
  storeSession(user)
  mockApi({
    ...layoutHandlers,
    'GET /api/v1/auth/me': user,
    'GET /api/v1/analysis/summary': makeSummary(),
  })
  return render(
    <Providers>
      <MemoryRouter>
        <UploadWizard />
      </MemoryRouter>
    </Providers>,
  )
}

const xls = () =>
  new File(['contenido'], 'libro_sintetico_3a.xls', { type: 'application/vnd.ms-excel' })

async function uploadValidFile() {
  await userEvent.upload(screen.getByLabelText('Archivo a subir'), xls())
  await userEvent.click(screen.getByRole('button', { name: 'Subir y analizar' }))
}

describe('UploadWizard', () => {
  beforeEach(() => mockedUpload.mockReset())

  it('valida la extensión en el cliente y no sube el archivo', async () => {
    renderWizard()
    const input = screen.getByLabelText('Archivo a subir')
    await userEvent.upload(input, new File(['x'], 'notas.pdf', { type: 'application/pdf' }), {
      applyAccept: false,
    })
    expect(screen.getByRole('alert')).toHaveTextContent('no es .xls, .xlsx ni .csv')
    expect(screen.getByRole('button', { name: 'Subir y analizar' })).toBeDisabled()
    expect(mockedUpload).not.toHaveBeenCalled()
  })

  it('sube, muestra las hojas detectadas y el resumen del análisis sin pasos manuales', async () => {
    mockedUpload.mockResolvedValue(makeReport())
    renderWizard()
    await uploadValidFile()

    expect(await screen.findByRole('heading', { name: 'Hojas detectadas' })).toBeInTheDocument()
    expect(screen.getByText('Balance general')).toBeInTheDocument()
    expect(screen.getByText('Hoja fiscal; se ignora.')).toBeInTheDocument()
    expect(screen.getByText('2022, 2023 y 2024')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Ver el resultado del análisis' }))
    expect(await screen.findByRole('heading', { name: 'Análisis completado' })).toBeInTheDocument()
    expect(await screen.findByText('13 de 14')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ir al dashboard' })).toHaveAttribute('href', '/')
    expect(screen.getByText('La nota 4 no cuadra con el balance.')).toBeInTheDocument()
    expect(screen.getByText('Hoja nota4 · fila 18 · columna G')).toBeInTheDocument()
    expect(screen.queryByText(/baja confianza/)).not.toBeInTheDocument()
  })

  it('recomienda revisar la homologación cuando hay cuentas dudosas', async () => {
    mockedUpload.mockResolvedValue(makeReport({ needsReview: true, doubtfulAccounts: 2 }))
    renderWizard()
    await uploadValidFile()
    await userEvent.click(
      await screen.findByRole('button', { name: 'Ver el resultado del análisis' }),
    )

    expect(
      await screen.findByText(/Algunas cuentas se clasificaron con baja confianza \(2\)/),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Revisar' })).toHaveAttribute(
      'href',
      '/mappings?uploadId=0196f000-0000-7000-8000-0000000000aa',
    )
  })

  it('un archivo con errores muestra mensajes claros y no permite continuar', async () => {
    mockedUpload.mockResolvedValue(
      makeReport({
        status: 'Failed',
        analysisStatus: 'NotStarted',
        errors: [
          {
            code: 'upload.no_financial_statements',
            message: 'No se encontró ni el balance general ni el estado de resultados.',
            sheet: null,
            row: null,
            column: null,
          },
        ],
      }),
    )
    renderWizard()
    await uploadValidFile()

    expect(
      await screen.findByRole('heading', { name: 'No se pudo usar el archivo' }),
    ).toBeInTheDocument()
    expect(screen.getByText(/No se encontró ni el balance general/)).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Ir al dashboard' })).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Subir otro archivo' }))
    expect(screen.getByRole('heading', { name: 'Sube el libro contable' })).toBeInTheDocument()
  })
})
