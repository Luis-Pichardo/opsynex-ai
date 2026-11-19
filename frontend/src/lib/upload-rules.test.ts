import { describe, expect, it } from 'vitest'

import { formatFileSize, formatYears } from './format'
import { checkFile } from './upload-rules'

const file = (name: string, size: number) => new File([new Uint8Array(size)], name)

describe('checkFile', () => {
  it('acepta .xls, .xlsx y .csv (sin importar mayúsculas)', () => {
    expect(checkFile(file('libro.XLS', 10))).toEqual({ ok: true, extension: '.xls' })
    expect(checkFile(file('plantilla.xlsx', 10)).ok).toBe(true)
    expect(checkFile(file('datos.csv', 10)).ok).toBe(true)
  })

  it('rechaza otras extensiones, archivos vacíos y los que superan el máximo', () => {
    expect(checkFile(file('informe.pdf', 10))).toMatchObject({ ok: false })
    expect(checkFile(file('vacio.xlsx', 0))).toMatchObject({
      ok: false,
      message: expect.stringContaining('vacío'),
    })
    expect(checkFile(file('grande.xlsx', 2 * 1024 * 1024), 1)).toMatchObject({
      ok: false,
      message: expect.stringContaining('1 MB'),
    })
  })
})

describe('formatos de la carga', () => {
  it('formatea tamaños y listas de años', () => {
    expect(formatFileSize(850 * 1024)).toBe('850 KB')
    expect(formatFileSize(2.4 * 1024 * 1024)).toBe('2,4 MB')
    expect(formatYears([2024, 2022, 2023])).toBe('2022, 2023 y 2024')
    expect(formatYears([2024])).toBe('2024')
    expect(formatYears([])).toBe('—')
  })
})
