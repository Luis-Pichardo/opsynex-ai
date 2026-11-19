import { Database, FileClock, UploadCloud } from 'lucide-react'
import { useSearchParams } from 'react-router'

import { PageHeader } from '@/components/common/PageHeader'
import { RequireCompany } from '@/components/common/RequireCompany'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { ConsolidatedData } from '@/components/uploads/ConsolidatedData'
import { UploadHistory } from '@/components/uploads/UploadHistory'
import { UploadWizard } from '@/components/uploads/UploadWizard'

const TABS = ['nueva', 'historial', 'datos'] as const
type Tab = (typeof TABS)[number]

/** Carga de datos: asistente de carga, historial y datos consolidados. */
export function UploadsPage() {
  const [params, setParams] = useSearchParams()
  const requested = params.get('tab')
  const tab: Tab = (TABS as readonly string[]).includes(requested ?? '')
    ? (requested as Tab)
    : 'nueva'

  return (
    <div className="space-y-6">
      <PageHeader
        title="Carga de datos"
        description="Sube el libro contable de la empresa: se lee, se valida y se analiza automáticamente."
      />
      <RequireCompany>
        <Tabs
          value={tab}
          onValueChange={(value) =>
            setParams(value === 'nueva' ? {} : { tab: value }, { replace: true })
          }
        >
          <TabsList>
            <TabsTrigger value="nueva">
              <UploadCloud aria-hidden="true" />
              Nueva carga
            </TabsTrigger>
            <TabsTrigger value="historial">
              <FileClock aria-hidden="true" />
              Historial
            </TabsTrigger>
            <TabsTrigger value="datos">
              <Database aria-hidden="true" />
              <span className="sm:hidden">Datos</span>
              <span className="hidden sm:inline">Datos consolidados</span>
            </TabsTrigger>
          </TabsList>
          <TabsContent value="nueva" forceMount className="data-[state=inactive]:hidden">
            <UploadWizard />
          </TabsContent>
          <TabsContent value="historial">
            <UploadHistory />
          </TabsContent>
          <TabsContent value="datos">
            <ConsolidatedData />
          </TabsContent>
        </Tabs>
      </RequireCompany>
    </div>
  )
}
