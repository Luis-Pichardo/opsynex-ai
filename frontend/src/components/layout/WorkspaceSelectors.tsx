import { Building2 } from 'lucide-react'
import { useId } from 'react'

import { useAuth } from '@/auth/useAuth'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { cn } from '@/lib/utils'
import { useWorkspace } from '@/workspace/useWorkspace'

/** Selector de empresa (solo Admin) y de año. */
export function WorkspaceSelectors({ className }: { className?: string }) {
  const { user } = useAuth()
  const { companies, companyId, companyName, setCompanyId, years, year, setYear } = useWorkspace()
  const isAdmin = user?.role === 'Admin'
  // Se renderiza dos veces (escritorio y móvil): ids únicos para cada etiqueta.
  const id = useId()

  return (
    <div className={cn('flex items-center gap-2', className)}>
      {isAdmin ? (
        <Select value={companyId ?? undefined} onValueChange={(id) => setCompanyId(id)}>
          {/* Etiqueta visible solo para lectores de pantalla: el nombre accesible no reemplaza
              el texto visible del selector (WCAG 2.5.3). */}
          <Label htmlFor={`${id}-company`} className="sr-only">
            Empresa
          </Label>
          <SelectTrigger id={`${id}-company`} className="min-w-0 flex-1 md:w-56 md:flex-none">
            <SelectValue placeholder="Elige una empresa" />
          </SelectTrigger>
          <SelectContent>
            {companies.length === 0 ? (
              <p className="px-2 py-2 text-small text-foreground-secondary">
                No hay empresas creadas.
              </p>
            ) : (
              companies.map((company) => (
                <SelectItem key={company.id} value={company.id}>
                  {company.name}
                </SelectItem>
              ))
            )}
          </SelectContent>
        </Select>
      ) : companyName ? (
        <span className="flex min-w-0 flex-1 items-center gap-2 text-small text-foreground-secondary md:flex-none">
          <Building2 className="size-4 shrink-0" aria-hidden="true" />
          <span className="truncate">{companyName}</span>
        </span>
      ) : null}
      <Select
        value={year === null ? undefined : String(year)}
        onValueChange={(value) => setYear(Number(value))}
        disabled={years.length === 0}
      >
        <Label htmlFor={`${id}-year`} className="sr-only">
          Año
        </Label>
        <SelectTrigger
          id={`${id}-year`}
          className="w-28 shrink-0"
          title={years.length === 0 ? 'Aún no hay años analizados' : undefined}
        >
          <SelectValue placeholder="Año" />
        </SelectTrigger>
        <SelectContent>
          {years.map((y) => (
            <SelectItem key={y} value={String(y)}>
              {y}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  )
}
