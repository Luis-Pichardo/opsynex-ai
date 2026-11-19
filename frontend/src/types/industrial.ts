/** Tipos del módulo industrial (`/industrial/kpis` y `/industrial/score`). */

import type { ScoreInterpretation } from './analysis'

export type IndustrialLevel = 'Total' | 'Line' | 'Machine'
export type IndustrialDimension = 'Production' | 'Cost' | 'Financial' | 'Standard'

export interface IndustrialFilter {
  year?: number | null
  month?: number | null
  machineId?: string | null
  lineId?: string | null
}

export interface IndustrialKpiValues {
  oee: number | null
  availability: number | null
  performance: number | null
  quality: number | null
  scrapRate: number | null
  downtimeRatio: number | null
  capacityUtilization: number | null
  costPerUnit: number | null
  standardCostPerUnit: number | null
}

export interface IndustrialCosts {
  labor: number | null
  material: number | null
  energy: number | null
  overhead: number | null
  total: number | null
}

export interface IndustrialLosses {
  downtimeCost: number | null
  scrapCost: number | null
  speedLossCost: number | null
  /** Paradas + scrap (fórmula de la especificación). */
  total: number | null
}

export interface IndustrialRow {
  period: string
  year: number
  month: number | null
  level: IndustrialLevel
  machineId: string | null
  lineId: string | null
  plannedMinutes: number
  downtimeMinutes: number
  totalUnits: number
  goodUnits: number
  unitsProduced: number
  days: number
  kpis: IndustrialKpiValues
  costs: IndustrialCosts | null
  losses: IndustrialLosses
}

export interface IndustrialDaily {
  date: string
  oee: number | null
  downtimeMinutes: number
  availability: number | null
  performance: number | null
  quality: number | null
}

export interface IndustrialMachine {
  machineId: string
  lineId: string | null
  processName: string | null
  hasStandard: boolean
  hasCosts: boolean
}

export interface IndustrialThresholds {
  lowOee: number
  highScrap: number
  highDowntime: number
  costTolerance: number
}

export interface IndustrialKpisResponse {
  companyId: string
  hasData: boolean
  currency: string
  years: number[]
  year: number | null
  month: number | null
  machineId: string | null
  lineId: string | null
  months: number[]
  machines: IndustrialMachine[]
  lines: string[]
  summary: IndustrialRow | null
  byMachine: IndustrialRow[]
  byLine: IndustrialRow[]
  monthly: IndustrialRow[]
  daily: IndustrialDaily[]
  thresholds: IndustrialThresholds
  warnings: string[]
}

export interface IndustrialScoreComponent {
  dimension: IndustrialDimension
  name: string
  score: number | null
  baseWeight: number
  weight: number
  inputs: Record<string, number | null>
  missingReason: string | null
}

export interface IndustrialScore {
  companyId: string
  hasData: boolean
  year: number | null
  month: number | null
  machineId: string | null
  lineId: string | null
  totalScore: number | null
  interpretation: ScoreInterpretation | null
  interpretationLabel: string | null
  weightsRedistributed: boolean
  components: IndustrialScoreComponent[]
  opsynexScore: number | null
  message: string | null
}
