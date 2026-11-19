import type { Industry } from '@/types/api'
import type { ImpactCategory, ProblemCategory, ProblemStatus } from '@/types/analysis'
import type { MappingMethod, SheetType, UploadFileType, UploadStatus } from '@/types/uploads'

/** Nombres en español de los valores del API. */
export const INDUSTRY_LABELS: Record<Industry, string> = {
  Manufacturing: 'Manufactura',
  Services: 'Servicios',
}

export const SHEET_TYPE_LABELS: Record<SheetType, string> = {
  Unknown: 'No reconocida',
  BalanceSheet: 'Balance general',
  IncomeStatement: 'Estado de resultados',
  EquityStatement: 'Cambios en el patrimonio',
  Notes: 'Notas',
  EmployeeRoster: 'Empleados',
  MonthlySales: 'Ventas mensuales',
  TrialBalance: 'Balance de comprobación',
  CashFlow: 'Flujo de efectivo',
  CostStructure: 'Estructura de costos',
  Operational: 'Datos operacionales',
}

export const FILE_TYPE_LABELS: Record<UploadFileType, string> = {
  AccountingWorkbook: 'Libro contable',
  IncomeStatement: 'Estado de resultados',
  BalanceSheet: 'Balance general',
  CashFlow: 'Flujo de efectivo',
  CostStructure: 'Estructura de costos',
  Operational: 'Datos operacionales',
}

/** Estado de una carga: variante del badge y texto. */
export const UPLOAD_STATUS: Record<
  UploadStatus,
  { label: string; variant: 'good' | 'info' | 'critical' | 'neutral' }
> = {
  Processed: { label: 'Analizada', variant: 'good' },
  Validated: { label: 'Validada', variant: 'info' },
  Failed: { label: 'Con errores', variant: 'critical' },
  Pending: { label: 'Pendiente', variant: 'neutral' },
}

/** Cómo se obtuvo la categoría de una cuenta. */
export const METHOD_LABELS: Record<MappingMethod, string> = {
  Manual: 'Confirmado',
  Known: 'Confirmado antes',
  Exact: 'Automático',
  Keyword: 'Automático',
  Rule: 'Automático',
  Fuzzy: 'Automático (aproximado)',
  Ai: 'IA',
  Fallback: 'Diccionario local',
  Unmatched: 'Sin clasificar',
}

/** Campos de `FinancialData` por grupo, en el orden en que se muestran. */
export const FINANCIAL_GROUPS: { title: string; fields: [string, string][] }[] = [
  {
    title: 'Estado de resultados',
    fields: [
      ['grossSales', 'Ventas brutas'],
      ['salesDiscounts', 'Descuentos y devoluciones'],
      ['revenue', 'Ingresos (ventas netas)'],
      ['cogs', 'Costo de ventas'],
      ['operatingExpenses', 'Gastos operacionales'],
      ['ebitda', 'EBITDA'],
      ['depreciation', 'Depreciación y amortización'],
      ['financialExpenses', 'Gastos financieros'],
      ['earningsBeforeTax', 'Utilidad antes de impuestos'],
      ['incomeTax', 'Impuesto sobre la renta'],
      ['netIncome', 'Utilidad neta'],
    ],
  },
  {
    title: 'Costos de detalle',
    fields: [
      ['totalCost', 'Costo total'],
      ['overheadCost', 'Gastos generales'],
      ['laborCost', 'Costo de personal'],
      ['energyCost', 'Energía'],
      ['maintenanceCost', 'Mantenimiento'],
      ['rawMaterials', 'Materias primas'],
      ['materialLoss', 'Pérdida de materiales'],
    ],
  },
  {
    title: 'Balance general',
    fields: [
      ['cash', 'Efectivo'],
      ['accountsReceivable', 'Cuentas por cobrar'],
      ['inventory', 'Inventarios'],
      ['currentAssets', 'Activos corrientes'],
      ['totalAssets', 'Activos totales'],
      ['accountsPayable', 'Cuentas por pagar'],
      ['currentLiabilities', 'Pasivos corrientes'],
      ['financialDebt', 'Deuda financiera'],
      ['totalLiabilities', 'Pasivos totales'],
      ['equity', 'Patrimonio'],
    ],
  },
]

export const IMPACT_CATEGORY_LABELS: Record<ImpactCategory, string> = {
  MarginLoss: 'Pérdida de margen',
  ExcessCost: 'Exceso de costos',
  ProductivityLoss: 'Pérdida de productividad',
  OperationalWaste: 'Desperdicio operacional',
  FinancialAlert: 'Alertas financieras',
  Anomaly: 'Anomalías',
}

export const PROBLEM_CATEGORY_LABELS: Record<ProblemCategory, string> = {
  Profitability: 'Rentabilidad',
  CostEfficiency: 'Eficiencia de costos',
  Productivity: 'Productividad',
  FinancialStability: 'Estabilidad financiera',
  Operations: 'Operaciones',
}

/** Estado de seguimiento de un problema: variante del badge y texto. */
export const PROBLEM_STATUS: Record<
  ProblemStatus,
  { label: string; variant: 'info' | 'neutral' | 'good' }
> = {
  Open: { label: 'Abierto', variant: 'info' },
  Acknowledged: { label: 'Reconocido', variant: 'neutral' },
  Resolved: { label: 'Resuelto', variant: 'good' },
}

export const EFFORT_LABELS = {
  low: 'Esfuerzo bajo',
  medium: 'Esfuerzo medio',
  high: 'Esfuerzo alto',
} as const

export const HORIZON_LABELS = {
  short: 'Corto plazo (menos de 3 meses)',
  medium: 'Mediano plazo (3 a 12 meses)',
  long: 'Largo plazo (más de 12 meses)',
} as const
