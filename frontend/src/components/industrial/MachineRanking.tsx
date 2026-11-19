import { Card } from '@/components/ui/card'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { formatCurrency, formatMinutes, formatPercent } from '@/lib/format'
import type { IndustrialRow, IndustrialThresholds } from '@/types/industrial'
import { ThresholdStatus } from './ThresholdStatus'

interface MachineRankingProps {
  rows: IndustrialRow[]
  thresholds: IndustrialThresholds
  currency: string
}

/** Ranking de máquinas por OEE (la peor primero) con sus componentes, scrap, paradas y pérdidas. */
export function MachineRanking({ rows, thresholds, currency }: MachineRankingProps) {
  const sorted = [...rows].sort(
    (a, b) => (a.kpis.oee ?? Number.POSITIVE_INFINITY) - (b.kpis.oee ?? Number.POSITIVE_INFINITY),
  )
  return (
    <Card className="overflow-hidden">
      <div className="space-y-0.5 p-4 sm:p-5">
        <h2 className="text-h3 text-foreground">Ranking de máquinas</h2>
        <p className="text-small text-foreground-secondary">
          Ordenadas por OEE, de la menos eficiente a la más eficiente. Pérdidas = paradas + scrap.
        </p>
      </div>
      <Table aria-label="Ranking de máquinas por OEE">
        <TableHeader>
          <TableRow className="hover:bg-transparent">
            <TableHead>#</TableHead>
            <TableHead>Máquina</TableHead>
            <TableHead className="numeric">OEE</TableHead>
            <TableHead>Estado</TableHead>
            <TableHead className="numeric">Disponibilidad</TableHead>
            <TableHead className="numeric">Rendimiento</TableHead>
            <TableHead className="numeric">Calidad</TableHead>
            <TableHead className="numeric">Scrap</TableHead>
            <TableHead className="numeric">Paradas</TableHead>
            <TableHead className="numeric">Pérdidas</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {sorted.map((row, index) => (
            <TableRow key={row.machineId}>
              <TableCell className="tabular-nums text-foreground-secondary">{index + 1}</TableCell>
              <TableCell className="whitespace-nowrap">
                <span className="font-medium text-foreground">{row.machineId}</span>
                {row.lineId ? (
                  <span className="block text-caption text-foreground-muted">{row.lineId}</span>
                ) : null}
              </TableCell>
              <TableCell className="numeric font-medium">{formatPercent(row.kpis.oee)}</TableCell>
              <TableCell>
                <ThresholdStatus
                  value={row.kpis.oee}
                  threshold={thresholds.lowOee}
                  higherIsBetter
                />
              </TableCell>
              <TableCell className="numeric">{formatPercent(row.kpis.availability)}</TableCell>
              <TableCell className="numeric">{formatPercent(row.kpis.performance)}</TableCell>
              <TableCell className="numeric">{formatPercent(row.kpis.quality)}</TableCell>
              <TableCell className="numeric">{formatPercent(row.kpis.scrapRate)}</TableCell>
              <TableCell className="numeric whitespace-nowrap">
                {formatMinutes(row.downtimeMinutes)}
                <span className="block text-caption text-foreground-muted">
                  {formatPercent(row.kpis.downtimeRatio)} del tiempo
                </span>
              </TableCell>
              <TableCell className="numeric whitespace-nowrap">
                {formatCurrency(row.losses.total, currency, { compact: true })}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Card>
  )
}
