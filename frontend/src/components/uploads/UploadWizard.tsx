import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight, CircleAlert, LayoutDashboard, RotateCcw, UploadCloud } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'

import { analysisApi } from '@/api/analysis'
import { errorMessage } from '@/api/client'
import { uploadFile } from '@/api/uploads'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Progress } from '@/components/ui/progress'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { formatNumber } from '@/lib/format'
import { FILE_TYPE_LABELS } from '@/lib/labels'
import { useWorkspace } from '@/workspace/useWorkspace'
import type { UploadFileType, UploadReport } from '@/types/uploads'
import { AnalysisSummaryCard } from './AnalysisSummaryCard'
import { FileDropzone } from './FileDropzone'
import { IssueList } from './IssueList'
import { ReviewNotice } from './ReviewNotice'
import { SheetList } from './SheetList'
import { Stepper } from './Stepper'
import { TemplateMenu } from './TemplateMenu'

const STEPS = ['Subir el archivo', 'Hojas detectadas', 'Resultado']
const AUTO = 'auto'
const CSV_TYPES: UploadFileType[] = [
  'IncomeStatement',
  'BalanceSheet',
  'CashFlow',
  'CostStructure',
  'Operational',
]

/**
 * Asistente de carga (FLUJO_DE_USUARIO.md): subir → hojas detectadas → resultado. El análisis
 * (homologación, KPIs, diagnóstico, impacto y score) lo hace el API al recibir el archivo; no hay
 * pasos manuales. La revisión de cuentas es opcional y solo se sugiere si hace falta.
 */
export function UploadWizard() {
  const queryClient = useQueryClient()
  const { companyId } = useWorkspace()
  const [step, setStep] = useState(1)
  const [file, setFile] = useState<File | null>(null)
  const [fileType, setFileType] = useState<string>(AUTO)
  const [progress, setProgress] = useState<number | null>(null)
  const [report, setReport] = useState<UploadReport | null>(null)

  const isCsv = file?.name.toLowerCase().endsWith('.csv') ?? false
  const failed = report?.status === 'Failed'

  const upload = useMutation({
    mutationFn: (selected: File) =>
      uploadFile(selected, {
        fileType: isCsv && fileType !== AUTO ? (fileType as UploadFileType) : undefined,
        onProgress: (fraction) => setProgress(Math.round(fraction * 100)),
      }),
    onMutate: () => setProgress(0),
    onSuccess: async (result) => {
      setReport(result)
      setStep(result.status === 'Failed' ? 3 : 2)
      // El análisis cambió KPIs, problemas, score y cargas: se refresca todo lo de la empresa.
      await queryClient.invalidateQueries()
    },
    onSettled: () => setProgress(null),
  })

  const summary = useQuery({
    queryKey: ['analysis', 'summary', companyId],
    queryFn: ({ signal }) => analysisApi.summary(signal),
    enabled: report !== null && !failed,
  })

  const reset = () => {
    setStep(1)
    setFile(null)
    setFileType(AUTO)
    setReport(null)
    upload.reset()
  }

  const uploading = upload.isPending
  const processing = uploading && progress === 100

  return (
    <div className="space-y-6">
      <Stepper steps={STEPS} current={step} failedStep={failed ? 3 : undefined} />

      {step === 1 ? (
        <Card className="space-y-5 p-4 sm:p-5">
          <div className="space-y-1">
            <h2 className="text-h2 text-foreground">Sube el libro contable</h2>
            <p className="text-body text-foreground-secondary">
              Sube tu libro de estados financieros tal como lo prepara tu contador. El análisis se
              hace solo: no hace falta borrar hojas ni confirmar nada.
            </p>
          </div>
          <FileDropzone file={file} onFileChange={setFile} disabled={uploading} />
          {isCsv ? (
            <div className="max-w-xs space-y-1.5">
              <Label htmlFor="csv-type">Tipo de plantilla</Label>
              <Select value={fileType} onValueChange={setFileType} disabled={uploading}>
                <SelectTrigger id="csv-type">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={AUTO}>Detectar por el título</SelectItem>
                  {CSV_TYPES.map((type) => (
                    <SelectItem key={type} value={type}>
                      {FILE_TYPE_LABELS[type]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-small text-foreground-secondary">
                Indícalo si el CSV no tiene en la primera fila el título de la plantilla.
              </p>
            </div>
          ) : null}
          {uploading ? (
            <div className="space-y-2" aria-live="polite">
              <Progress
                value={processing ? null : progress}
                label={processing ? 'Analizando el archivo' : 'Subiendo el archivo'}
              />
              <p className="text-small text-foreground-secondary">
                {processing
                  ? 'Analizando: leyendo hojas, validando, clasificando cuentas y calculando KPIs, problemas y score…'
                  : `Subiendo… ${formatNumber(progress ?? 0)} %`}
              </p>
            </div>
          ) : null}
          {upload.isError ? (
            <p
              role="alert"
              className="flex items-start gap-2 rounded border border-critical bg-critical-soft p-3 text-small text-critical-text"
            >
              <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
              {errorMessage(upload.error)}
            </p>
          ) : null}
          <div className="flex flex-col-reverse gap-3 sm:flex-row sm:items-center sm:justify-between">
            <TemplateMenu />
            <Button
              onClick={() => file && upload.mutate(file)}
              disabled={!file}
              loading={uploading}
            >
              <UploadCloud />
              Subir y analizar
            </Button>
          </div>
        </Card>
      ) : null}

      {step === 2 && report ? (
        <Card className="space-y-5 p-4 sm:p-5">
          <div className="space-y-1">
            <h2 className="text-h2 text-foreground">Hojas detectadas</h2>
            <p className="text-body text-foreground-secondary">
              Así se reconoció cada hoja de «{report.fileName}». Las hojas fiscales o de trabajo se
              ignoran.
            </p>
          </div>
          <SheetList report={report} />
          <div className="flex justify-end">
            <Button onClick={() => setStep(3)}>
              Ver el resultado del análisis
              <ArrowRight />
            </Button>
          </div>
        </Card>
      ) : null}

      {step === 3 && report ? (
        <div className="space-y-6">
          {failed ? (
            <Card className="space-y-5 p-4 sm:p-5">
              <div className="space-y-1">
                <h2 className="text-h2 text-foreground">No se pudo usar el archivo</h2>
                <p className="text-body text-foreground-secondary">
                  Corrige lo siguiente y vuelve a subirlo. No se guardó ningún dato de este archivo.
                </p>
              </div>
              <IssueList kind="error" issues={report.errors} />
              <div className="flex flex-col-reverse gap-3 sm:flex-row sm:items-center sm:justify-between">
                <TemplateMenu label="Descargar una plantilla" />
                <Button onClick={reset}>
                  <RotateCcw />
                  Subir otro archivo
                </Button>
              </div>
            </Card>
          ) : (
            <>
              <section className="space-y-3" aria-labelledby="summary-title">
                <div className="flex flex-wrap items-end justify-between gap-3">
                  <div className="space-y-1">
                    <h2 id="summary-title" className="text-h2 text-foreground">
                      Análisis completado
                    </h2>
                    <p className="text-body text-foreground-secondary">
                      Resultado del análisis automático de «{report.fileName}».
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button variant="outline" onClick={reset}>
                      <RotateCcw />
                      Subir otro archivo
                    </Button>
                    <Button asChild>
                      <Link to="/">
                        <LayoutDashboard />
                        Ir al dashboard
                      </Link>
                    </Button>
                  </div>
                </div>
                {report.analysisStatus === 'Failed' ? (
                  <p
                    role="alert"
                    className="rounded border border-critical bg-critical-soft p-3 text-small text-critical-text"
                  >
                    Los datos se cargaron, pero el análisis no terminó. Vuelve a analizar desde la
                    homologación.
                  </p>
                ) : null}
                {report.needsReview ? (
                  <ReviewNotice uploadId={report.uploadId} count={report.doubtfulAccounts} />
                ) : null}
                <AnalysisSummaryCard summary={summary.data} loading={summary.isLoading} />
              </section>
              <IssueList kind="warning" issues={report.warnings} />
            </>
          )}
        </div>
      ) : null}
    </div>
  )
}
