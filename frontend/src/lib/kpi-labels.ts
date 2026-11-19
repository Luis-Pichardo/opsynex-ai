import type { KpiCode } from '@/types/analysis'

/** Fórmulas en español para los tooltips (GUIA_DE_DISENO.md §9: "Margen EBITDA" → "EBITDA / Ingresos"). */
export const KPI_FORMULAS: Record<KpiCode, string> = {
  EbitdaMargin: 'EBITDA / Ingresos',
  GrossMargin: '(Ingresos − Costo de ventas) / Ingresos',
  CostEfficiency: 'Costo total / Ingresos (bajar es favorable)',
  RevenuePerEmployee: 'Ingresos / Número de empleados',
  OperatingCostRatio: 'Gastos operacionales / Ingresos',
  OverheadRatio: 'Gastos generales / Ingresos',
  EnergyCostRatio: 'Costo de energía / Ingresos',
  CostPerUnit: 'Costo total / Unidades producidas',
  NetMargin: 'Utilidad neta / Ingresos',
  LaborCostRatio: 'Costo de personal / Ingresos',
  CurrentRatio: 'Activos corrientes / Pasivos corrientes',
  DebtToEquity: 'Pasivos totales / Patrimonio',
  DaysSalesOutstanding: 'Cuentas por cobrar / Ingresos × 365',
  InventoryDays: 'Inventario / Costo de ventas × 365',
}

/** KPIs de la fila principal del dashboard. */
export const DASHBOARD_KPIS: KpiCode[] = [
  'EbitdaMargin',
  'GrossMargin',
  'CostEfficiency',
  'RevenuePerEmployee',
  'OperatingCostRatio',
]
