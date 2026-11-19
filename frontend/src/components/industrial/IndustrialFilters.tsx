import { X } from 'lucide-react'
import type { ReactNode } from 'react'

import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { formatMonth } from '@/lib/format'
import type { IndustrialFilter, IndustrialKpisResponse } from '@/types/industrial'

const ALL = 'all'

interface IndustrialFiltersProps {
  data: IndustrialKpisResponse
  filter: IndustrialFilter
  onChange: (filter: IndustrialFilter) => void
}

/** Filtros de las páginas industriales: año, mes, línea y máquina (se guardan en la URL). */
export function IndustrialFilters({ data, filter, onChange }: IndustrialFiltersProps) {
  const lineMachines = data.machines.filter((m) => !filter.lineId || m.lineId === filter.lineId)
  const active = Boolean(filter.month || filter.lineId || filter.machineId || filter.year)
  return (
    <div
      className="grid gap-3 sm:grid-cols-2 lg:flex lg:flex-wrap lg:items-end"
      role="group"
      aria-label="Filtros"
    >
      {data.years.length > 1 ? (
        <Field id="industrial-year" label="Año">
          <Select
            value={String(data.year)}
            onValueChange={(v) => onChange({ ...filter, year: Number(v), month: null })}
          >
            <SelectTrigger id="industrial-year" className="lg:w-32">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {data.years.map((y) => (
                <SelectItem key={y} value={String(y)}>
                  {y}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>
      ) : null}
      <Field id="industrial-month" label="Período">
        <Select
          value={filter.month ? String(filter.month) : ALL}
          onValueChange={(v) => onChange({ ...filter, month: v === ALL ? null : Number(v) })}
        >
          <SelectTrigger id="industrial-month" className="lg:w-44">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Todo {data.year}</SelectItem>
            {data.months.map((m) => (
              <SelectItem key={m} value={String(m)}>
                {formatMonth(data.year, m)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>
      <Field id="industrial-line" label="Línea">
        <Select
          value={filter.lineId ?? ALL}
          onValueChange={(v) =>
            onChange({ ...filter, lineId: v === ALL ? null : v, machineId: null })
          }
        >
          <SelectTrigger id="industrial-line" className="lg:w-44">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Todas las líneas</SelectItem>
            {data.lines.map((line) => (
              <SelectItem key={line} value={line}>
                {line}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>
      <Field id="industrial-machine" label="Máquina">
        <Select
          value={filter.machineId ?? ALL}
          onValueChange={(v) => onChange({ ...filter, machineId: v === ALL ? null : v })}
        >
          <SelectTrigger id="industrial-machine" className="lg:w-44">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Todas las máquinas</SelectItem>
            {lineMachines.map((m) => (
              <SelectItem key={m.machineId} value={m.machineId}>
                {m.machineId}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>
      {active ? (
        <Button variant="ghost" className="justify-self-start" onClick={() => onChange({})}>
          <X />
          Quitar filtros
        </Button>
      ) : null}
    </div>
  )
}

function Field({ id, label, children }: { id: string; label: string; children: ReactNode }) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      {children}
    </div>
  )
}
