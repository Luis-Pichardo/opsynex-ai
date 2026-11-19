import type { ColumnDef } from '@tanstack/react-table'
import { FileUp, Inbox, Plus, Trash2 } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'

import { EmptyState } from '@/components/common/EmptyState'
import { PageHeader } from '@/components/common/PageHeader'
import { ScoreGauge } from '@/components/common/ScoreGauge'
import { SeverityBadge } from '@/components/common/SeverityBadge'
import { StatCard } from '@/components/common/StatCard'
import { TrendIndicator } from '@/components/common/TrendIndicator'
import { ThemeToggle } from '@/components/layout/ThemeToggle'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { DataTable } from '@/components/ui/data-table'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { SimpleTooltip } from '@/components/ui/tooltip'
import { formatCurrency, formatPercent } from '@/lib/format'

const SWATCHES = [
  ['bg-background', '--background'],
  ['bg-surface', '--surface'],
  ['bg-surface-muted', '--surface-muted'],
  ['bg-sidebar', '--sidebar'],
  ['bg-primary', '--primary'],
  ['bg-primary-soft', '--primary-soft'],
  ['bg-border', '--border'],
  ['bg-foreground', '--foreground'],
] as const

const SEMANTIC = [
  ['bg-critical', 'bg-critical-soft text-critical-text', 'Crítico'],
  ['bg-serious', 'bg-serious-soft text-serious-text', 'Serio'],
  ['bg-warning', 'bg-warning-soft text-warning-text', 'Advertencia'],
  ['bg-good', 'bg-good-soft text-good-text', 'Bueno'],
  ['bg-info', 'bg-info-soft text-info-text', 'Informativo'],
] as const

const CHARTS = [
  'bg-chart-1',
  'bg-chart-2',
  'bg-chart-3',
  'bg-chart-4',
  'bg-chart-5',
  'bg-chart-6',
  'bg-chart-7',
  'bg-chart-8',
] as const

const TYPE_SCALE = [
  ['text-display', 'Display 48'],
  ['text-h1', 'H1 30'],
  ['text-h2', 'H2 22'],
  ['text-h3', 'H3 18'],
  ['text-kpi', 'KPI 28'],
  ['text-body', 'Body 15'],
  ['text-small', 'Small 13'],
  ['text-caption', 'Caption 12'],
  ['text-overline uppercase', 'Overline 11'],
] as const

interface DemoRow {
  account: string
  category: string
  amount: number
  share: number
}

const DEMO_ROWS: DemoRow[] = [
  { account: 'Ventas netas', category: 'Ingresos', amount: 181_741_000, share: 1 },
  { account: 'Costo de ventas', category: 'COGS', amount: 130_766_000, share: 0.7195 },
  {
    account: 'Gastos de administración',
    category: 'Gastos operacionales',
    amount: 24_310_500,
    share: 0.1338,
  },
  { account: 'Energía eléctrica', category: 'Energía', amount: 11_213_000, share: 0.0617 },
]

const DEMO_COLUMNS: ColumnDef<DemoRow, unknown>[] = [
  { accessorKey: 'account', header: 'Cuenta' },
  { accessorKey: 'category', header: 'Categoría' },
  {
    accessorKey: 'amount',
    header: 'Monto',
    meta: { numeric: true },
    cell: ({ row }) => formatCurrency(row.original.amount, 'DOP'),
  },
  {
    accessorKey: 'share',
    header: '% ingresos',
    meta: { numeric: true },
    cell: ({ row }) => formatPercent(row.original.share),
  },
]

function Block({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="space-y-4">
      <h2 className="text-h2 text-foreground">{title}</h2>
      {children}
    </section>
  )
}

/** Catálogo visual del sistema de diseño (solo en desarrollo: `/design`). */
export function DesignPage() {
  const form = useForm({ defaultValues: { email: 'nombre@empresa', name: '' } })
  const [loading, setLoading] = useState(false)

  return (
    <div className="mx-auto max-w-content space-y-8 px-4 py-8 lg:px-8">
      <PageHeader
        title="Sistema de diseño"
        description="Componentes y tokens de GUIA_DE_DISENO.md. Revisa en modo claro y oscuro y a 375 px."
        actions={<ThemeToggle />}
      />

      <Block title="Color">
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {SWATCHES.map(([cls, name]) => (
            <div key={name} className="space-y-1">
              <div className={`h-12 rounded border border-border ${cls}`} />
              <p className="font-mono text-caption text-foreground-secondary">{name}</p>
            </div>
          ))}
        </div>
        <div className="grid gap-3 sm:grid-cols-5">
          {SEMANTIC.map(([base, soft, label]) => (
            <div key={label} className="space-y-1">
              <div className={`h-6 rounded-sm ${base}`} />
              <div className={`rounded-sm px-2 py-1 text-caption ${soft}`}>{label}</div>
            </div>
          ))}
        </div>
        <div className="flex flex-wrap gap-2" aria-label="Paleta de gráficos en orden fijo">
          {CHARTS.map((cls, i) => (
            <div key={cls} className="flex items-center gap-2">
              <span className={`size-6 rounded-sm ${cls}`} />
              <span className="text-caption text-foreground-secondary">{i + 1}</span>
            </div>
          ))}
        </div>
      </Block>

      <Block title="Tipografía">
        <Card className="space-y-2 p-5">
          {TYPE_SCALE.map(([cls, label]) => (
            <p key={label} className={`${cls} text-foreground`}>
              {label} · Margen EBITDA
            </p>
          ))}
        </Card>
      </Block>

      <Block title="Botones">
        <div className="flex flex-wrap gap-3">
          <Button>
            <FileUp />
            Subir archivo
          </Button>
          <Button variant="secondary">Secundario</Button>
          <Button variant="outline">Outline</Button>
          <Button variant="ghost">Ghost</Button>
          <Button variant="destructive">
            <Trash2 />
            Eliminar
          </Button>
          <Button variant="link">Enlace</Button>
          <Button size="sm">Pequeño</Button>
          <Button size="lg">Grande</Button>
          <Button
            loading={loading}
            onClick={() => {
              setLoading(true)
              setTimeout(() => setLoading(false), 1500)
            }}
          >
            Ejecutar análisis
          </Button>
          <SimpleTooltip content="Sube un archivo primero">
            <span tabIndex={0}>
              <Button disabled>Deshabilitado</Button>
            </span>
          </SimpleTooltip>
        </div>
      </Block>

      <Block title="Badges y severidad">
        <div className="flex flex-wrap gap-2">
          <Badge variant="critical">Crítico</Badge>
          <Badge variant="serious">Serio</Badge>
          <Badge variant="warning">Advertencia</Badge>
          <Badge variant="good">Bueno</Badge>
          <Badge variant="info">Informativo</Badge>
          <Badge>Neutro</Badge>
          <SeverityBadge severity="High" />
          <SeverityBadge severity="Medium" />
          <SeverityBadge severity="Low" />
        </div>
        <div className="flex flex-wrap gap-6">
          <TrendIndicator value={4.2} higherIsBetter suffix=" vs. 2023" />
          <TrendIndicator value={-3.2} higherIsBetter={false} />
          <TrendIndicator value={-1.14} unit="pp" higherIsBetter />
          <TrendIndicator value={0.4} higherIsBetter />
          <TrendIndicator value={null} higherIsBetter />
        </div>
      </Block>

      <Block title="Score y tarjetas">
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {[92, 70, 45, null].map((score) => (
            <Card key={String(score)} className="p-5">
              <ScoreGauge score={score} />
            </Card>
          ))}
        </div>
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <StatCard
            label="Margen EBITDA"
            hint="EBITDA / Ingresos"
            value={formatPercent(0.1861)}
            trend={{ value: -1.14, unit: 'pp', higherIsBetter: true, suffix: ' vs. 2023' }}
          />
          <StatCard
            label="Impacto estimado"
            value={formatCurrency(4_397_941, 'DOP', { compact: true })}
          />
          <StatCard
            label="Eficiencia de costos"
            hint="Costo total / Ingresos"
            value={formatPercent(0.8533)}
            trend={{ value: 1.3, higherIsBetter: false }}
          />
          <StatCard label="Cargando" value="" loading />
        </div>
      </Block>

      <Block title="Formularios">
        <Card className="max-w-md p-5">
          <Form {...form}>
            <form
              className="space-y-4"
              onSubmit={form.handleSubmit(() => toast.success('Formulario válido.'))}
              noValidate
            >
              <FormField
                control={form.control}
                name="email"
                rules={{ pattern: { value: /^\S+@\S+\.\S+$/, message: 'El correo no es válido.' } }}
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Correo</FormLabel>
                    <FormControl>
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="name"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Nombre</FormLabel>
                    <FormControl>
                      <Input placeholder="Empresa Demo S.R.L." {...field} />
                    </FormControl>
                    <FormDescription>Texto de ayuda bajo el campo.</FormDescription>
                  </FormItem>
                )}
              />
              <div className="space-y-1.5">
                <Label htmlFor="design-select">Industria</Label>
                <Select defaultValue="Manufacturing">
                  <SelectTrigger id="design-select">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Manufacturing">Manufactura</SelectItem>
                    <SelectItem value="Services">Servicios</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <Button type="submit">Validar</Button>
            </form>
          </Form>
        </Card>
      </Block>

      <Block title="Tabla">
        <DataTable columns={DEMO_COLUMNS} data={DEMO_ROWS} pageSize={3} />
      </Block>

      <Block title="Navegación y superposiciones">
        <Tabs defaultValue="resumen">
          <TabsList>
            <TabsTrigger value="resumen">Resumen</TabsTrigger>
            <TabsTrigger value="detalle">Detalle</TabsTrigger>
            <TabsTrigger value="historial">Historial</TabsTrigger>
          </TabsList>
          <TabsContent value="resumen" className="text-body text-foreground-secondary">
            Contenido de la pestaña activa.
          </TabsContent>
          <TabsContent value="detalle">Detalle</TabsContent>
          <TabsContent value="historial">Historial</TabsContent>
        </Tabs>
        <div className="flex flex-wrap gap-3">
          <Dialog>
            <DialogTrigger asChild>
              <Button variant="outline">Abrir diálogo</Button>
            </DialogTrigger>
            <DialogContent>
              <DialogHeader>
                <DialogTitle>Confirmar mapeo</DialogTitle>
                <DialogDescription>
                  Los botones van abajo a la derecha: secundario y luego primario.
                </DialogDescription>
              </DialogHeader>
              <DialogFooter>
                <Button variant="outline">Cancelar</Button>
                <Button>Confirmar</Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
          <Sheet>
            <SheetTrigger asChild>
              <Button variant="outline">Abrir panel</Button>
            </SheetTrigger>
            <SheetContent className="p-6">
              <SheetTitle>Panel lateral</SheetTitle>
              <SheetDescription className="text-foreground-secondary">
                Sheet con overlay y transición de 200 ms.
              </SheetDescription>
            </SheetContent>
          </Sheet>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline">Menú</Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent>
              <DropdownMenuLabel>Acciones</DropdownMenuLabel>
              <DropdownMenuItem>
                <Plus aria-hidden="true" />
                Agregar
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem destructive>
                <Trash2 aria-hidden="true" />
                Eliminar
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
          <SimpleTooltip content="Margen EBITDA = EBITDA / Ingresos">
            <Button variant="ghost">Tooltip</Button>
          </SimpleTooltip>
          <Button variant="secondary" onClick={() => toast.success('Análisis completado.')}>
            Toast de éxito
          </Button>
          <Button
            variant="secondary"
            onClick={() =>
              toast.error(
                'El archivo no tiene la columna «2024». Descarga la plantilla y vuelve a intentarlo.',
                {
                  duration: Infinity,
                },
              )
            }
          >
            Toast de error
          </Button>
        </div>
      </Block>

      <Block title="Carga y estados vacíos">
        <Card className="space-y-3 p-5">
          <Skeleton className="h-6 w-48" />
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-2/3" />
        </Card>
        <Card>
          <EmptyState
            icon={Inbox}
            title="Todavía no hay datos"
            description="Sube el libro contable de la empresa para ver el análisis."
            action={
              <Button>
                <FileUp />
                Subir archivo
              </Button>
            }
          />
        </Card>
      </Block>
    </div>
  )
}
