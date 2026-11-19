import type { AnalysisSummary, PendingAccount, UploadReport } from '@/types/uploads'

/** Datos de prueba sintéticos (sin datos de clientes). */
export function makeReport(overrides: Partial<UploadReport> = {}): UploadReport {
  return {
    uploadId: '0196f000-0000-7000-8000-0000000000aa',
    companyId: '0196f000-0000-7000-8000-000000000001',
    fileName: 'libro_sintetico_3a.xls',
    fileType: 'AccountingWorkbook',
    status: 'Processed',
    years: [2022, 2023, 2024],
    sheets: [
      {
        name: 'BALANCE',
        detectedType: 'BalanceSheet',
        rowsExtracted: 20,
        ignored: false,
        reason: null,
      },
      {
        name: 'g&P',
        detectedType: 'IncomeStatement',
        rowsExtracted: 16,
        ignored: false,
        reason: null,
      },
      {
        name: 'ANEXOA1',
        detectedType: 'Unknown',
        rowsExtracted: 0,
        ignored: true,
        reason: 'Hoja fiscal; se ignora.',
      },
    ],
    errors: [],
    warnings: [
      {
        code: 'quality.note_mismatch',
        message: 'La nota 4 no cuadra con el balance.',
        sheet: 'nota4',
        row: 18,
        column: 'G',
      },
    ],
    summary: {
      employeeCount: 15,
      annualPayroll: 1000,
      monthlyRecords: 12,
      trialBalanceReliable: true,
      trialBalanceDifference: 0,
    },
    rowsCount: 412,
    createdAt: '2026-11-19T10:00:00Z',
    analysisStatus: 'Completed',
    needsReview: false,
    doubtfulAccounts: 0,
    ...overrides,
  }
}

export function makeSummary(overrides: Partial<AnalysisSummary> = {}): AnalysisSummary {
  return {
    companyId: '0196f000-0000-7000-8000-000000000001',
    currency: 'DOP',
    years: [2022, 2023, 2024],
    latestYear: 2024,
    kpisCalculated: 13,
    kpisTotal: 14,
    problems: { total: 4, high: 0, medium: 1, low: 3 },
    score: 64.2,
    interpretation: 'Improvable',
    interpretationLabel: 'Oportunidades de mejora',
    estimatedLoss: 4_400_000,
    calculatedAt: '2026-11-19T10:00:00Z',
    ...overrides,
  }
}

export function makeAccount(overrides: Partial<PendingAccount> = {}): PendingAccount {
  return {
    mappingId: 'm-1',
    clientAccount: 'Partida especial ZQX',
    originalLabel: 'Partida especial ZQX',
    sheetName: 'g&P',
    sheetType: 'IncomeStatement',
    rowNumber: 26,
    section: null,
    scope: 'Statement',
    role: 'Primary',
    category: 'Other',
    confidence: 0,
    method: 'Unmatched',
    isConfirmed: false,
    isDoubtful: true,
    year: 2024,
    amount: 125_000,
    ...overrides,
  }
}

export const CATEGORIES = [
  { category: 'Revenue', name: 'Ingresos', description: '', statement: 'IncomeStatement' },
  {
    category: 'OperatingExpenses',
    name: 'Gastos operacionales',
    description: '',
    statement: 'IncomeStatement',
  },
  { category: 'Cash', name: 'Efectivo', description: '', statement: 'BalanceSheet' },
  { category: 'Other', name: 'Otros', description: '', statement: 'None' },
] as const
