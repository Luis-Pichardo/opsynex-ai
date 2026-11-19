import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  CheckCheck,
  CircleCheck,
  FileUp,
  LayoutDashboard,
  RefreshCw,
  Search,
  Undo2,
} from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { toast } from 'sonner'

import { analysisApi } from '@/api/analysis'
import { mappingsApi } from '@/api/mappings'
import { uploadsApi } from '@/api/uploads'
import { EmptyState } from '@/components/common/EmptyState'
import { PageHeader } from '@/components/common/PageHeader'
import { RequireCompany } from '@/components/common/RequireCompany'
import { MappingReviewTable } from '@/components/mappings/MappingReviewTable'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { AnalysisSummaryCard } from '@/components/uploads/AnalysisSummaryCard'
import { formatDate, formatNumber } from '@/lib/format'
import { useWorkspace } from '@/workspace/useWorkspace'
import type { AnalysisSummary, ProcessingResult } from '@/types/uploads'

type Filter = 'doubtful' | 'all'

interface Outcome {
  doubtfulBefore: number
  previous: AnalysisSummary
  current: AnalysisSummary
  result: ProcessingResult
}

/** Homologación (opcional): revisar y corregir la categoría de las cuentas y volver a analizar. */
export function MappingsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Homologación"
        description="Revisión opcional: corrige la categoría de las cuentas que el sistema clasificó con dudas. Lo que confirmes se recuerda en las próximas cargas."
      />
      <RequireCompany>
        <MappingReview />
      </RequireCompany>
    </div>
  )
}

function MappingReview() {
  const queryClient = useQueryClient()
  const { companyId, currency } = useWorkspace()
  const [params, setParams] = useSearchParams()
  const [filter, setFilter] = useState<Filter | null>(null)
  const [search, setSearch] = useState('')
  const [edits, setEdits] = useState<Record<string, string>>({})
  const [outcome, setOutcome] = useState<Outcome | null>(null)

  const uploads = useQuery({
    queryKey: ['uploads', 'list', companyId],
    queryFn: ({ signal }) => uploadsApi.list(signal),
  })
  const usable = useMemo(
    () => (uploads.data ?? []).filter((u) => u.status !== 'Failed'),
    [uploads.data],
  )
  const uploadId =
    params.get('uploadId') ?? (usable.find((u) => u.needsReview) ?? usable[0])?.id ?? null

  const pending = useQuery({
    queryKey: ['mappings', 'pending', uploadId],
    queryFn: ({ signal }) => mappingsApi.pending(uploadId!, signal),
    enabled: uploadId !== null,
  })
  const categories = useQuery({
    queryKey: ['mappings', 'categories'],
    queryFn: ({ signal }) => mappingsApi.categories(signal),
    staleTime: Infinity,
  })

  const accounts = useMemo(() => pending.data ?? [], [pending.data])
  const doubtful = accounts.filter((a) => a.isDoubtful).length
  const activeFilter: Filter = filter ?? (doubtful > 0 ? 'doubtful' : 'all')
  const visible = useMemo(() => {
    const term = search.trim().toLowerCase()
    return accounts.filter(
      (a) =>
        (activeFilter === 'all' || a.isDoubtful) &&
        (term === '' ||
          a.originalLabel.toLowerCase().includes(term) ||
          a.sheetName.toLowerCase().includes(term)),
    )
  }, [accounts, activeFilter, search])

  const onChange = useCallback(
    (id: string, category: string) => setEdits((current) => ({ ...current, [id]: category })),
    [],
  )
  const changes = Object.keys(edits).length

  const confirmAll = () =>
    setEdits((current) => {
      const next = { ...current }
      for (const a of visible) if (!a.isConfirmed) next[a.mappingId] ??= a.category
      return next
    })

  const save = useMutation({
    mutationFn: async () => {
      const previous = await queryClient.fetchQuery({
        queryKey: ['analysis', 'summary', companyId],
        queryFn: ({ signal }) => analysisApi.summary(signal),
        staleTime: 0,
      })
      await mappingsApi.save(Object.entries(edits).map(([id, category]) => ({ id, category })))
      const result = await mappingsApi.consolidate(uploadId!)
      await queryClient.invalidateQueries()
      const current = await analysisApi.summary()
      return { doubtfulBefore: doubtful, previous, current, result }
    },
    onSuccess: (data) => {
      setEdits({})
      setOutcome(data)
      toast.success('Cuentas guardadas y análisis actualizado.')
      window.scrollTo({ top: 0, behavior: 'smooth' })
    },
  })

  if (uploads.isLoading) return <Skeleton className="h-96" />

  if (usable.length === 0) {
    return (
      <Card>
        <EmptyState
          icon={FileUp}
          title="Todavía no hay cuentas para revisar"
          description="Sube primero el libro contable de la empresa. Si alguna cuenta queda con dudas, el sistema te lo avisará."
          action={
            <Button asChild>
              <Link to="/uploads">
                <FileUp />
                Ir a Carga de datos
              </Link>
            </Button>
          }
        />
      </Card>
    )
  }

  return (
    <div className="space-y-6">
      {outcome ? (
        <section className="space-y-3" aria-labelledby="outcome-title" aria-live="polite">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <h2 id="outcome-title" className="flex items-center gap-2 text-h2 text-foreground">
              <CircleCheck className="size-5 text-good-text" aria-hidden="true" />
              Análisis actualizado
            </h2>
            <Button asChild>
              <Link to="/">
                <LayoutDashboard />
                Ir al dashboard
              </Link>
            </Button>
          </div>
          <AnalysisSummaryCard
            summary={outcome.current}
            previous={outcome.previous}
            extraChanges={[
              {
                label: 'Cuentas dudosas',
                before: formatNumber(outcome.doubtfulBefore),
                after: formatNumber(outcome.result.doubtfulAccounts),
              },
            ]}
          />
          {outcome.result.needsReview ? (
            <p className="text-small text-foreground-secondary">
              Quedan {formatNumber(outcome.result.doubtfulAccounts)} cuentas con dudas. Puedes
              revisarlas abajo o dejarlas así.
            </p>
          ) : (
            <p className="text-small text-foreground-secondary">
              No quedan cuentas dudosas en esta carga.
            </p>
          )}
        </section>
      ) : null}

      <Card className="space-y-4 p-4 sm:p-5">
        <div className="grid gap-3 md:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1fr)]">
          <div className="space-y-1.5">
            <Label htmlFor="mapping-upload">Carga</Label>
            <Select
              value={uploadId ?? undefined}
              onValueChange={(id) => {
                setParams({ uploadId: id }, { replace: true })
                setEdits({})
                setOutcome(null)
                setFilter(null)
              }}
            >
              <SelectTrigger id="mapping-upload">
                <SelectValue placeholder="Elige una carga" />
              </SelectTrigger>
              <SelectContent>
                {usable.map((u) => (
                  <SelectItem key={u.id} value={u.id}>
                    {u.fileName} · {formatDate(u.createdAt)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="mapping-filter">Mostrar</Label>
            <Select value={activeFilter} onValueChange={(v) => setFilter(v as Filter)}>
              <SelectTrigger id="mapping-filter">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="doubtful">Solo dudosas ({formatNumber(doubtful)})</SelectItem>
                <SelectItem value="all">Todas ({formatNumber(accounts.length)})</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="mapping-search">Buscar</Label>
            <div className="relative">
              <Search
                className="pointer-events-none absolute left-3 top-3 size-4 text-foreground-muted"
                aria-hidden="true"
              />
              <Input
                id="mapping-search"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Cuenta u hoja"
                className="pl-9"
              />
            </div>
          </div>
        </div>

        {pending.isLoading || categories.isLoading ? (
          <Skeleton className="h-80" />
        ) : (
          <MappingReviewTable
            accounts={visible}
            categories={categories.data ?? []}
            currency={currency}
            edits={edits}
            onChange={onChange}
            disabled={save.isPending}
            emptyTitle={
              activeFilter === 'doubtful' && search === ''
                ? 'No hay cuentas dudosas en esta carga'
                : 'Ninguna cuenta coincide con la búsqueda'
            }
          />
        )}

        <div className="flex flex-col gap-3 border-t border-border pt-4 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-small text-foreground-secondary" aria-live="polite">
            {changes === 0
              ? 'Cambia una categoría o confirma las sugerencias para guardar.'
              : `${formatNumber(changes)} ${changes === 1 ? 'cuenta por confirmar' : 'cuentas por confirmar'}.`}
          </p>
          <div className="flex flex-col gap-2 sm:flex-row">
            {changes > 0 ? (
              <Button variant="ghost" onClick={() => setEdits({})} disabled={save.isPending}>
                <Undo2 />
                Descartar cambios
              </Button>
            ) : null}
            <Button
              variant="outline"
              onClick={confirmAll}
              disabled={save.isPending || visible.every((a) => a.isConfirmed)}
            >
              <CheckCheck />
              Confirmar todas
            </Button>
            <Button onClick={() => save.mutate()} disabled={changes === 0} loading={save.isPending}>
              <RefreshCw />
              Guardar y volver a analizar
            </Button>
          </div>
        </div>
      </Card>
    </div>
  )
}
