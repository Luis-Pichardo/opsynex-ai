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

// --- Dashboard (etapa 10) ---

const DEFINITIONS = [
  {
    code: 'EbitdaMargin',
    name: 'Margen EBITDA',
    formula: 'EBITDA / Revenue',
    unit: 'Ratio',
    higherIsBetter: true,
    dimension: 'Profitability',
    description: '',
  },
  {
    code: 'GrossMargin',
    name: 'Margen bruto',
    formula: '',
    unit: 'Ratio',
    higherIsBetter: true,
    dimension: 'Profitability',
    description: '',
  },
  {
    code: 'CostEfficiency',
    name: 'Eficiencia de costos',
    formula: '',
    unit: 'Ratio',
    higherIsBetter: false,
    dimension: 'CostEfficiency',
    description: '',
  },
  {
    code: 'RevenuePerEmployee',
    name: 'Ingresos por empleado',
    formula: '',
    unit: 'Money',
    higherIsBetter: true,
    dimension: 'Productivity',
    description: '',
  },
  {
    code: 'OperatingCostRatio',
    name: 'Gastos operacionales / ingresos',
    formula: '',
    unit: 'Ratio',
    higherIsBetter: false,
    dimension: 'CostEfficiency',
    description: '',
  },
  {
    code: 'CurrentRatio',
    name: 'Razón corriente',
    formula: '',
    unit: 'Times',
    higherIsBetter: true,
    dimension: 'FinancialStability',
    description: '',
  },
] as const

const values = (
  ebitda: number,
  gross: number,
  cost: number,
  rpe: number | null,
  opex: number,
  current: number,
) => ({
  EbitdaMargin: ebitda,
  GrossMargin: gross,
  CostEfficiency: cost,
  RevenuePerEmployee: rpe,
  OperatingCostRatio: opex,
  CurrentRatio: current,
})

export function makeKpis() {
  return {
    companyId: 'c',
    industry: 'Manufacturing',
    currency: 'DOP',
    definitions: DEFINITIONS,
    years: [
      {
        year: 2023,
        values: values(0.1975, 0.2897, 0.842, null, 0.1318, 2.38),
        normalized: {},
        trends: null,
        calculatedAt: '2026-11-19T10:00:00Z',
      },
      {
        year: 2024,
        values: values(0.1861, 0.2805, 0.8533, 12_116_066.67, 0.1338, 2.77),
        normalized: {},
        trends: {
          year: 2024,
          previousYear: 2023,
          kpis: {
            EbitdaMargin: {
              previous: 0.1975,
              current: 0.1861,
              change: -0.0114,
              changePercent: -5.77,
              direction: 'Down',
              favorable: false,
            },
            CostEfficiency: {
              previous: 0.842,
              current: 0.8533,
              change: 0.0113,
              changePercent: 1.34,
              direction: 'Up',
              favorable: false,
            },
            CurrentRatio: {
              previous: 2.38,
              current: 2.77,
              change: 0.39,
              changePercent: 16.4,
              direction: 'Up',
              favorable: true,
            },
          },
          revenueGrowth: null,
          totalCostGrowth: null,
          overheadGrowth: null,
        },
        calculatedAt: '2026-11-19T10:00:00Z',
      },
    ],
  }
}

export function makeScore() {
  const component = (dimension: string, name: string, score: number | null, weight: number) => ({
    dimension,
    name,
    score,
    baseWeight: weight,
    weight,
    inputs: {},
    missingReason: score === null ? 'Falta la cantidad de empleados.' : null,
  })
  return {
    companyId: 'c',
    years: [
      {
        year: 2024,
        totalScore: 78.4,
        interpretation: 'Improvable',
        interpretationLabel: 'Oportunidades de mejora',
        changeFromPrevious: -2.3,
        weightsRedistributed: false,
        weightsUsed: {},
        components: [
          component('Profitability', 'Rentabilidad', 77, 0.3),
          component('CostEfficiency', 'Eficiencia de costos', 73, 0.25),
          component('Productivity', 'Productividad', 79, 0.25),
          component('FinancialStability', 'Estabilidad financiera', 86, 0.2),
        ],
        calculatedAt: '2026-11-19T10:00:00Z',
      },
    ],
  }
}

export function makeImpact() {
  return {
    companyId: 'c',
    year: 2024,
    currency: 'DOP',
    totalEstimatedLoss: 4_397_941.56,
    revenue: 181_741_000,
    lossPercentOfRevenue: 2.42,
    quantifiedProblems: 4,
    unquantifiedProblems: 0,
    byCategory: [
      { category: 'MarginLoss', estimatedLoss: 3_743_864.6, problems: 2, share: 85.13 },
      { category: 'ExcessCost', estimatedLoss: 654_076.96, problems: 2, share: 14.87 },
    ],
    top: [],
  }
}

export function makeProblem(overrides: Record<string, unknown> = {}) {
  return {
    id: 'p-1',
    year: 2024,
    code: 'EBITDA_DECLINE',
    title: 'Deterioro de rentabilidad',
    description: 'El margen EBITDA bajó de 19,75 % en 2023 a 18,61 % en 2024 (−1,14 pp).',
    category: 'Profitability',
    severity: 'Low',
    impactCategory: 'MarginLoss',
    impactValue: 2_071_847.4,
    status: 'Open',
    recommendation:
      'Para recuperar el margen EBITDA, revisa qué costos crecieron más que los ingresos.',
    priority: 1,
    ...overrides,
  }
}

export function makeProblemPage(
  items = [
    makeProblem(),
    makeProblem({
      id: 'p-2',
      code: 'OVERHEAD_OUTPACES_REVENUE',
      title: 'Ineficiencia administrativa',
      category: 'CostEfficiency',
      impactCategory: 'ExcessCost',
      impactValue: 12_904.08,
      priority: 2,
    }),
  ],
) {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    highlights: [
      {
        code: 'DebtToEquity',
        title: 'Menor endeudamiento',
        detail: 'Los pasivos bajaron de 0,6 a 0,47 veces el patrimonio.',
      },
    ],
  }
}

export function makeProblemDetail(overrides: Record<string, unknown> = {}) {
  const item: Record<string, unknown> = { ...makeProblem() }
  delete item.priority
  return {
    ...item,
    aiRecommendation: null,
    evidence: {
      kpi: 'EbitdaMargin',
      year: 2024,
      previous: 0.1975,
      current: 0.1861,
      changePp: -1.14,
      impact: {
        estimatedLoss: 2_071_847.4,
        affectedMarginPp: 1.14,
        deteriorationMagnitude: 5.77,
        severityLevel: 'Low',
        formula: '(margen anterior − margen actual) × ingresos actuales',
        reason: null,
        inputs: { previousMargin: 0.1975, currentMargin: 0.1861, revenue: 181_741_000 },
      },
    },
    relatedKpi: 'EbitdaMargin',
    kpiHistory: [
      { year: 2022, value: 0.228, normalized: 100 },
      { year: 2023, value: 0.1975, normalized: 98.75 },
      { year: 2024, value: 0.1861, normalized: 93.05 },
    ],
    updatedAt: '2026-11-19T10:00:00Z',
    ...overrides,
  }
}
