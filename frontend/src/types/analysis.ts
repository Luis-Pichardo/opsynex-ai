/** Tipos de KPIs, score, impacto y problemas (`/kpis`, `/score`, `/impact`, `/problems`). */

export type KpiCode =
  | 'EbitdaMargin'
  | 'GrossMargin'
  | 'CostEfficiency'
  | 'RevenuePerEmployee'
  | 'OperatingCostRatio'
  | 'OverheadRatio'
  | 'EnergyCostRatio'
  | 'CostPerUnit'
  | 'NetMargin'
  | 'LaborCostRatio'
  | 'CurrentRatio'
  | 'DebtToEquity'
  | 'DaysSalesOutstanding'
  | 'InventoryDays'

export type KpiUnit = 'Ratio' | 'Times' | 'Money' | 'Days'
export type KpiDimension =
  'Profitability' | 'CostEfficiency' | 'Productivity' | 'FinancialStability'

export interface KpiDefinition {
  code: KpiCode
  name: string
  formula: string
  unit: KpiUnit
  higherIsBetter: boolean
  dimension: KpiDimension
  description: string
}

export interface Trend {
  previous: number | null
  current: number | null
  change: number | null
  changePercent: number | null
  direction: 'Up' | 'Down' | 'Stable' | null
  favorable: boolean | null
}

export interface YearTrends {
  year: number
  previousYear: number
  kpis: Partial<Record<KpiCode, Trend>>
  revenueGrowth: Trend | null
  totalCostGrowth: Trend | null
  overheadGrowth: Trend | null
}

export interface KpiYear {
  year: number
  values: Record<KpiCode, number | null>
  normalized: Record<string, number | null>
  trends: YearTrends | null
  calculatedAt: string
}

export interface KpisResponse {
  companyId: string
  industry: 'Manufacturing' | 'Services'
  currency: string
  definitions: KpiDefinition[]
  years: KpiYear[]
}

export type ScoreInterpretation = 'Healthy' | 'Improvable' | 'Deteriorated'

export interface ScoreComponent {
  dimension: KpiDimension
  name: string
  score: number | null
  baseWeight: number
  weight: number
  inputs: Record<string, number | null>
  missingReason: string | null
}

export interface ScoreYear {
  year: number
  totalScore: number
  interpretation: ScoreInterpretation
  interpretationLabel: string
  changeFromPrevious: number | null
  weightsRedistributed: boolean
  weightsUsed: Record<string, number>
  components: ScoreComponent[]
  calculatedAt: string
}

export interface ScoreResponse {
  companyId: string
  years: ScoreYear[]
}

export type ImpactCategory =
  'MarginLoss' | 'ExcessCost' | 'ProductivityLoss' | 'OperationalWaste' | 'FinancialAlert'
export type Severity = 'High' | 'Medium' | 'Low'
export type ProblemCategory =
  'Profitability' | 'CostEfficiency' | 'Productivity' | 'FinancialStability' | 'Operations'
export type ProblemStatus = 'Open' | 'Acknowledged' | 'Resolved'

export interface ImpactSummary {
  companyId: string
  year: number | null
  currency: string
  totalEstimatedLoss: number
  revenue: number | null
  lossPercentOfRevenue: number | null
  quantifiedProblems: number
  unquantifiedProblems: number
  byCategory: {
    category: ImpactCategory
    estimatedLoss: number
    problems: number
    share: number | null
  }[]
  top: {
    id: string
    year: number
    code: string
    title: string
    severity: Severity
    impactCategory: ImpactCategory
    impactValue: number
    affectedMarginPp: number | null
  }[]
}

export interface Highlight {
  code: string
  title: string
  detail: string
}

export interface ProblemListItem {
  id: string
  year: number
  code: string
  title: string
  description: string
  category: ProblemCategory
  severity: Severity
  impactCategory: ImpactCategory
  impactValue: number | null
  status: ProblemStatus
  recommendation: string
  priority: number
}

export interface ProblemPage {
  items: ProblemListItem[]
  page: number
  pageSize: number
  totalCount: number
  highlights: Highlight[]
}

/** Cálculo del impacto guardado en `evidence.impact`. */
export interface ImpactEvidence {
  estimatedLoss: number | null
  affectedMarginPp: number | null
  deteriorationMagnitude: number | null
  severityLevel: Severity
  formula: string
  reason: string | null
  inputs: Record<string, number | string | null>
}

export interface ProblemDetail extends Omit<ProblemListItem, 'priority'> {
  aiRecommendation: string | null
  evidence: Record<string, unknown> & { impact?: ImpactEvidence }
  relatedKpi: KpiCode | null
  kpiHistory: { year: number; value: number | null; normalized: number | null }[]
  updatedAt: string
}

export interface ProblemQuery {
  year?: number
  severity?: Severity
  category?: ProblemCategory
  status?: ProblemStatus
  page?: number
  pageSize?: number
}
