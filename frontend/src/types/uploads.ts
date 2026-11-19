/** Tipos de carga, homologación, análisis y datos (`/uploads`, `/mappings`, `/analysis`, `/data`). */

export type UploadFileType =
  | 'AccountingWorkbook'
  | 'IncomeStatement'
  | 'BalanceSheet'
  | 'CashFlow'
  | 'CostStructure'
  | 'Operational'

export type SheetType =
  | 'Unknown'
  | 'BalanceSheet'
  | 'IncomeStatement'
  | 'EquityStatement'
  | 'Notes'
  | 'EmployeeRoster'
  | 'MonthlySales'
  | 'TrialBalance'
  | 'CashFlow'
  | 'CostStructure'
  | 'Operational'

export type UploadStatus = 'Pending' | 'Validated' | 'Failed' | 'Processed'
export type AnalysisStatus = 'NotStarted' | 'Completed' | 'Failed'
export type MappingMethod =
  'Manual' | 'Exact' | 'Fuzzy' | 'Ai' | 'Keyword' | 'Rule' | 'Known' | 'Fallback' | 'Unmatched'
export type MappingScope = 'Statement' | 'Notes'
export type MappingRole = 'Primary' | 'Breakdown'
export type FinancialStatement = 'IncomeStatement' | 'BalanceSheet' | 'CashFlow' | 'None'

export interface UploadSheet {
  name: string
  detectedType: SheetType
  rowsExtracted: number
  ignored: boolean
  reason: string | null
}

export interface UploadIssue {
  code: string
  message: string
  sheet: string | null
  row: number | null
  column: string | null
}

export interface UploadSummary {
  employeeCount: number | null
  annualPayroll: number | null
  monthlyRecords: number
  trialBalanceReliable: boolean | null
  trialBalanceDifference: number | null
}

export interface UploadReport {
  uploadId: string
  companyId: string
  fileName: string
  fileType: UploadFileType
  status: UploadStatus
  years: number[]
  sheets: UploadSheet[]
  errors: UploadIssue[]
  warnings: UploadIssue[]
  summary: UploadSummary
  rowsCount: number
  createdAt: string
  analysisStatus: AnalysisStatus
  needsReview: boolean
  doubtfulAccounts: number
}

export interface UploadListItem {
  id: string
  companyId: string
  fileName: string
  fileType: UploadFileType
  status: UploadStatus
  years: number[]
  rowsCount: number
  warningsCount: number
  errorsCount: number
  createdAt: string
  analysisStatus: AnalysisStatus
  needsReview: boolean
  uploadedBy: string | null
}

export interface CategoryOption {
  category: string
  name: string
  description: string
  statement: FinancialStatement
}

export interface PendingAccount {
  mappingId: string
  clientAccount: string
  originalLabel: string
  sheetName: string
  sheetType: SheetType
  rowNumber: number
  section: string | null
  scope: MappingScope
  role: MappingRole
  category: string
  confidence: number
  method: MappingMethod
  isConfirmed: boolean
  isDoubtful: boolean
  year: number
  amount: number
}

export interface ProcessingResult {
  uploadId: string
  status: UploadStatus
  analysisStatus: AnalysisStatus
  needsReview: boolean
  doubtfulAccounts: number
  usedFallback: boolean
  years: number[]
}

export interface AnalysisSummary {
  companyId: string
  currency: string
  years: number[]
  latestYear: number | null
  kpisCalculated: number
  kpisTotal: number
  problems: { total: number; high: number; medium: number; low: number }
  score: number | null
  interpretation: 'Healthy' | 'Improvable' | 'Deteriorated' | null
  interpretationLabel: string | null
  estimatedLoss: number | null
  calculatedAt: string | null
}

export type FinancialRecord = { year: number } & Record<string, number | string | null>

export interface OperationalRecord {
  year: number
  unitsProduced: number | null
  employeeCount: number | null
  scrapEstimate: number | null
  energyCost: number | null
  operationalCosts: number | null
  energyConsumptionKwh: number | null
}

export interface DataResponse {
  companyId: string
  type: 'Raw' | 'Financial' | 'Operational' | 'Monthly'
  year: number | null
  availableYears: number[]
  financial?: FinancialRecord[] | null
  operational?: OperationalRecord[] | null
}
