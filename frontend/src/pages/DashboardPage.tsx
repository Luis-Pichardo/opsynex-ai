import { useQuery } from '@tanstack/react-query'
import { FileUp, LayoutDashboard } from 'lucide-react'
import { Link } from 'react-router'

import { dataApi } from '@/api/analysis'
import { kpisApi } from '@/api/kpis'
import { impactApi, problemsApi, scoreApi } from '@/api/results'
import { ROLES, hasRole } from '@/auth/permissions'
import { useAuth } from '@/auth/useAuth'
import { DownloadReportButton } from '@/components/common/DownloadReportButton'
import { EmptyState } from '@/components/common/EmptyState'
import { PageHeader } from '@/components/common/PageHeader'
import { RequireCompany } from '@/components/common/RequireCompany'
import { ExecutiveSummaryCard } from '@/components/dashboard/ExecutiveSummaryCard'
import { ImpactCard } from '@/components/dashboard/ImpactCard'
import { KpiCards } from '@/components/dashboard/KpiCards'
import { ScoreCard } from '@/components/dashboard/ScoreCard'
import { Recommendations, TopProblems } from '@/components/dashboard/TopProblems'
import { TrendCharts } from '@/components/dashboard/TrendCharts'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useWorkspace } from '@/workspace/useWorkspace'

/** Dashboard ejecutivo (GUIA_DE_DISENO.md §5): score e impacto → KPIs → tendencias → problemas y recomendaciones. */
export function DashboardPage() {
  const { year } = useWorkspace()
  return (
    <div className="space-y-6 lg:space-y-8">
      <PageHeader
        title="Dashboard"
        description={
          year
            ? `Salud financiera y operacional de la empresa en ${year}.`
            : 'Salud financiera y operacional de la empresa.'
        }
        actions={<DownloadReportButton year={year} />}
      />
      <RequireCompany>
        <Dashboard />
      </RequireCompany>
    </div>
  )
}

function Dashboard() {
  const { user } = useAuth()
  const { companyId, year, currency } = useWorkspace()
  const enabled = companyId !== null && year !== null

  const kpis = useQuery({
    queryKey: ['kpis', 'all', companyId],
    queryFn: ({ signal }) => kpisApi.get(null, signal),
    enabled: companyId !== null,
  })
  const score = useQuery({
    queryKey: ['score', companyId],
    queryFn: ({ signal }) => scoreApi.get(null, signal),
    enabled,
  })
  const impact = useQuery({
    queryKey: ['impact', companyId, year],
    queryFn: ({ signal }) => impactApi.summary(year, signal),
    enabled,
  })
  const problems = useQuery({
    queryKey: ['problems', 'top', companyId, year],
    queryFn: ({ signal }) => problemsApi.list({ year: year ?? undefined, pageSize: 5 }, signal),
    enabled,
  })
  const financial = useQuery({
    queryKey: ['data', 'Financial', companyId, 'all'],
    queryFn: ({ signal }) => dataApi.get('Financial', null, signal),
    enabled,
  })

  if (kpis.isLoading) {
    return (
      <div className="space-y-4">
        <div className="grid gap-4 lg:grid-cols-12">
          <Skeleton className="h-80 lg:col-span-5" />
          <Skeleton className="h-80 lg:col-span-7" />
        </div>
        <Skeleton className="h-28" />
      </div>
    )
  }

  if (!kpis.data || kpis.data.years.length === 0 || year === null) {
    return (
      <Card>
        <EmptyState
          icon={LayoutDashboard}
          title="Sube tus estados financieros para empezar"
          description={
            hasRole(user, ROLES.analyze)
              ? 'Cuando subas el libro contable de la empresa verás aquí el score, el impacto económico, los KPIs y los principales problemas.'
              : 'Cuando el analista cargue la información de tu empresa verás aquí el score, los KPIs y los principales problemas.'
          }
          action={
            hasRole(user, ROLES.analyze) ? (
              <Button asChild>
                <Link to="/uploads">
                  <FileUp />
                  Subir archivo
                </Link>
              </Button>
            ) : null
          }
        />
      </Card>
    )
  }

  const kpiYear = kpis.data.years.find((y) => y.year === year)
  const scoreYear = score.data?.years.find((y) => y.year === year)
  const topProblems = problems.data?.items ?? []

  return (
    <div className="space-y-4 lg:space-y-6">
      <section className="grid gap-3 sm:gap-4 lg:grid-cols-12" aria-label="Score e impacto">
        <div className="lg:col-span-5">
          <ScoreCard score={scoreYear} loading={score.isLoading} />
        </div>
        <div className="lg:col-span-7">
          <ImpactCard impact={impact.data} loading={impact.isLoading} />
        </div>
      </section>
      {companyId ? <ExecutiveSummaryCard companyId={companyId} year={year} /> : null}
      <section aria-label="KPIs principales">
        <KpiCards definitions={kpis.data.definitions} year={kpiYear} currency={currency} />
      </section>
      <section aria-label="Tendencias">
        <TrendCharts
          definitions={kpis.data.definitions}
          years={kpis.data.years}
          financial={financial.data?.financial ?? []}
          currency={currency}
        />
      </section>
      <section
        className="grid gap-3 sm:gap-4 lg:grid-cols-12"
        aria-label="Problemas y recomendaciones"
      >
        <div className="lg:col-span-7">
          <TopProblems
            problems={topProblems}
            total={problems.data?.totalCount ?? 0}
            currency={currency}
            loading={problems.isLoading}
          />
        </div>
        <div className="lg:col-span-5">
          <Recommendations
            problems={topProblems}
            highlights={problems.data?.highlights ?? []}
            loading={problems.isLoading}
          />
        </div>
      </section>
    </div>
  )
}
