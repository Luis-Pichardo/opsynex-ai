import type { IndustrialKpisResponse, IndustrialRow, IndustrialScore } from '@/types/industrial'

/** Datos industriales sintéticos (planta ficticia: 3 máquinas, 2 líneas, oct–dic 2024). */
export function makeIndustrialRow(overrides: Partial<IndustrialRow> = {}): IndustrialRow {
  return {
    period: '2024',
    year: 2024,
    month: null,
    level: 'Machine',
    machineId: 'MAQ-01',
    lineId: 'LINEA-1',
    plannedMinutes: 74_880,
    downtimeMinutes: 3_144,
    totalUnits: 136_000,
    goodUnits: 133_200,
    unitsProduced: 133_200,
    days: 78,
    kpis: {
      oee: 0.8942,
      availability: 0.958,
      performance: 0.9528,
      quality: 0.9796,
      scrapRate: 0.0204,
      downtimeRatio: 0.042,
      capacityUtilization: 0.91,
      costPerUnit: 31.87,
      standardCostPerUnit: 32.5,
    },
    costs: {
      labor: 930_000,
      material: 2_460_000,
      energy: 425_000,
      overhead: 430_000,
      total: 4_245_000,
    },
    losses: { downtimeCost: 179_208, scrapCost: 91_000, speedLossCost: 120_000, total: 270_208 },
    ...overrides,
  }
}

const maq2 = makeIndustrialRow({
  machineId: 'MAQ-02',
  downtimeMinutes: 5_826,
  kpis: {
    oee: 0.8029,
    availability: 0.9222,
    performance: 0.947,
    quality: 0.9194,
    scrapRate: 0.0806,
    downtimeRatio: 0.0778,
    capacityUtilization: 0.87,
    costPerUnit: 35.35,
    standardCostPerUnit: 30,
  },
  losses: { downtimeCost: 384_516, scrapCost: 365_070, speedLossCost: 90_000, total: 749_586 },
})

const maq3 = makeIndustrialRow({
  machineId: 'MAQ-03',
  lineId: 'LINEA-2',
  downtimeMinutes: 18_656,
  kpis: {
    oee: 0.5672,
    availability: 0.7509,
    performance: 0.7829,
    quality: 0.9648,
    scrapRate: 0.0352,
    downtimeRatio: 0.2491,
    capacityUtilization: 0.59,
    costPerUnit: 43.42,
    standardCostPerUnit: 41,
  },
  losses: { downtimeCost: 783_552, scrapCost: 104_000, speedLossCost: 514_000, total: 887_552 },
})

export function makeIndustrialKpis(
  overrides: Partial<IndustrialKpisResponse> = {},
): IndustrialKpisResponse {
  const months = [10, 11, 12]
  return {
    companyId: '0196f000-0000-7000-8000-000000000001',
    hasData: true,
    currency: 'DOP',
    years: [2024],
    year: 2024,
    month: null,
    machineId: null,
    lineId: null,
    months,
    machines: [
      {
        machineId: 'MAQ-01',
        lineId: 'LINEA-1',
        processName: 'Inyección',
        hasStandard: true,
        hasCosts: true,
      },
      {
        machineId: 'MAQ-02',
        lineId: 'LINEA-1',
        processName: 'Inyección',
        hasStandard: true,
        hasCosts: true,
      },
      {
        machineId: 'MAQ-03',
        lineId: 'LINEA-2',
        processName: 'Ensamble',
        hasStandard: true,
        hasCosts: true,
      },
    ],
    lines: ['LINEA-1', 'LINEA-2'],
    summary: makeIndustrialRow({
      level: 'Total',
      machineId: null,
      lineId: null,
      plannedMinutes: 224_640,
      downtimeMinutes: 27_626,
      kpis: {
        oee: 0.7512,
        availability: 0.877,
        performance: 0.9,
        quality: 0.951,
        scrapRate: 0.049,
        downtimeRatio: 0.123,
        capacityUtilization: 0.79,
        costPerUnit: 36.2,
        standardCostPerUnit: 33.4,
      },
      losses: {
        downtimeCost: 1_347_276,
        scrapCost: 560_070,
        speedLossCost: 724_000,
        total: 1_907_346,
      },
    }),
    byMachine: [makeIndustrialRow(), maq2, maq3],
    byLine: [],
    monthly: months.map((month) =>
      makeIndustrialRow({
        level: 'Total',
        machineId: null,
        lineId: null,
        month,
        period: `2024-${month}`,
      }),
    ),
    daily: [
      {
        date: '2024-11-13',
        oee: 0.76,
        downtimeMinutes: 330,
        availability: 0.88,
        performance: 0.9,
        quality: 0.96,
      },
      {
        date: '2024-11-14',
        oee: 0.41,
        downtimeMinutes: 1_340,
        availability: 0.53,
        performance: 0.85,
        quality: 0.91,
      },
    ],
    thresholds: { lowOee: 0.6, highScrap: 0.05, highDowntime: 0.15, costTolerance: 0.1 },
    warnings: [],
    ...overrides,
  }
}

export function makeIndustrialScore(overrides: Partial<IndustrialScore> = {}): IndustrialScore {
  return {
    companyId: '0196f000-0000-7000-8000-000000000001',
    hasData: true,
    year: 2024,
    month: null,
    machineId: null,
    lineId: null,
    totalScore: 68.4,
    interpretation: 'Improvable',
    interpretationLabel: 'Oportunidades de mejora',
    weightsRedistributed: false,
    components: [
      {
        dimension: 'Production',
        name: 'Producción (OEE)',
        score: 78.04,
        baseWeight: 0.3,
        weight: 0.3,
        inputs: {},
        missingReason: null,
      },
      {
        dimension: 'Cost',
        name: 'Costo frente al estándar',
        score: 72,
        baseWeight: 0.25,
        weight: 0.25,
        inputs: {},
        missingReason: null,
      },
      {
        dimension: 'Financial',
        name: 'Score financiero',
        score: 61,
        baseWeight: 0.25,
        weight: 0.25,
        inputs: {},
        missingReason: null,
      },
      {
        dimension: 'Standard',
        name: 'Cumplimiento de estándares',
        score: 60,
        baseWeight: 0.2,
        weight: 0.2,
        inputs: {},
        missingReason: null,
      },
    ],
    opsynexScore: 61,
    message: null,
    ...overrides,
  }
}

export const NO_INDUSTRIAL_DATA = makeIndustrialKpis({
  hasData: false,
  years: [],
  year: null,
  months: [],
  machines: [],
  lines: [],
  summary: null,
  byMachine: [],
  monthly: [],
  daily: [],
})
