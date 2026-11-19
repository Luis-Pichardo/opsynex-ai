## Frontend

Tecnología: React + TypeScript + Vite + Tailwind CSS + **shadcn/ui** (componentes accesibles
basados en Radix), iconos lucide y gráficos Recharts.

### Sistema de diseño

La interfaz sigue la guía `docs/diseno/GUIA_DE_DISENO.md` (vista previa en
`docs/diseno/guia_visual.html`):

- **Color de marca:** azul índigo `#3B4FB8`, con barra lateral azul noche `#141C3D`.
- **Fondos neutros cálidos** y modo claro y oscuro.
- **Colores semánticos reservados:** rojo (severidad alta / deterioro), naranja (media), ámbar
  (baja / mejorable) y verde (saludable), siempre acompañados de icono y texto.
- **Paleta de gráficos de 8 colores** validada para daltonismo, asignada en orden fijo por entidad.
- **Tipografía Inter** con cifras tabulares para los montos y KPIs.

### Estructura (`frontend/src/`)

| Carpeta | Contenido |
|---|---|
| `api/` | Cliente HTTP (`client.ts`) y funciones por recurso: `auth`, `companies`, `users`, `kpis` |
| `auth/` | `AuthProvider` (sesión JWT), `useAuth`, `ProtectedRoute`, `RoleGuard`, `permissions.ts` (roles por política) |
| `workspace/` | `WorkspaceProvider`: empresa elegida (Admin) y año seleccionado |
| `theme/` | `ThemeProvider`: claro, oscuro o sistema (`data-theme` en `<html>`) |
| `components/ui/` | Componentes shadcn/ui adaptados a la guía: Button, Input, Select, Label, Card, Badge, Table, DataTable (TanStack Table), Tabs, Dialog, Sheet, DropdownMenu, Tooltip, Skeleton, Toaster (sonner), Form (React Hook Form + Zod) |
| `components/common/` | Componentes propios: `StatCard`, `EmptyState`, `PageHeader`, `ScoreGauge`, `SeverityBadge`, `TrendIndicator`, `ConfirmDialog` |
| `components/layout/` | `AppLayout`, `Sidebar`, `Topbar`, `WorkspaceSelectors`, `ThemeToggle`, `UserMenu`, `navigation.ts` |
| `components/admin/` | Diálogos de empresa, usuario y contraseña |
| `pages/` | Login, secciones (Dashboard, Problemas, KPIs, Carga, Homologación), Administración, `/design`, 404 |
| `router/routes.tsx` | Definición de rutas y permisos |
| `lib/` | `format.ts` (único lugar de formato), `query-client.ts`, `storage.ts`, `score.ts`, `trend.ts`, `password.ts`, `form-errors.ts`, `utils.ts` (`cn`) |
| `styles/tokens.css` | Copia exacta de `docs/diseno/tokens.css` (una prueba lo verifica) |
| `types/api.ts` | Tipos de los DTO del API |

### Sistema de diseño implementado

- **Tokens:** `tokens.css` define colores, radios, sombras y fuentes como variables CSS para el
  modo claro y el oscuro. `tailwind.config.ts` los mapea a clases (`bg-surface`,
  `text-critical-text`, `shadow-md`, `text-kpi`, `w-sidebar`…). Los componentes usan solo esas clases.
- **Modo oscuro:** lo resuelven los tokens. `ThemeProvider` pone `data-theme="light|dark"` en
  `<html>` o lo quita para seguir al sistema, y recuerda la preferencia (`opsynex.theme`).
- **Verificación automática:** `src/styles/design-tokens.test.ts` falla si aparece un color hex,
  `rgb()`, un tamaño de fuente o una sombra arbitraria fuera de `tokens.css`.
- **`tailwind-merge`** está extendido con la escala tipográfica de la guía. Sin eso, `text-body`
  se confundía con un color y borraba el color del texto del botón.
- **Componentes semánticos:** `Badge` incluye el icono de su variante (`critical`, `serious`,
  `warning`, `good`, `info`). `SeverityBadge` traduce High/Medium/Low a Alta/Media/Baja.
  `TrendIndicator` separa la dirección (flecha) del significado (color), con texto para lectores
  de pantalla. `ScoreGauge` dibuja un arco de 240° con el color del rango y la etiqueta.
- **Página `/design`** (solo con `npm run dev`): catálogo de tokens y componentes en ambos modos.
  No se incluye en el build de producción.

### Rutas y permisos

| Ruta | Pantalla | Roles |
|---|---|---|
| `/login` | Inicio de sesión (sin registro) | Público |
| `/` | Dashboard | Todos |
| `/problems` | Problemas | Todos |
| `/kpis` | KPIs | Todos |
| `/uploads` | Carga de datos | Admin, Analyst |
| `/mappings` | Homologación | Admin, Analyst |
| `/admin/companies`, `/admin/users` | Administración | Admin |
| `/design` | Sistema de diseño | Solo en desarrollo |

- `ProtectedRoute` exige sesión. Sin sesión lleva a `/login` y, después de entrar, vuelve a la
  página pedida.
- `RoleGuard` protege las rutas por rol, también si se entra por URL directa: muestra "No tienes
  acceso a esta sección". El menú lateral solo muestra lo que el rol permite (`navigation.ts`).
- "Producción" aparece deshabilitada con un tooltip hasta el módulo industrial (etapa 12).
- La administración y `/design` se cargan de forma diferida (code splitting).

### Sesión y manejo de estado

- **Sesión:** `POST /auth/login` devuelve el JWT y su vencimiento. `AuthProvider` lo guarda en
  `localStorage` (`opsynex.session`), lo envía en `Authorization: Bearer` y:
  - al abrir la app con una sesión guardada, refresca el usuario con `GET /auth/me`;
  - cierra la sesión al llegar `expiresAt` (temporizador) o cuando el API responde 401, con el
    aviso "Tu sesión expiró";
  - el 401 del propio login no cierra nada: muestra "Correo o contraseña incorrectos".
- **Datos del servidor:** TanStack Query. Los errores ProblemDetails se muestran como toast
  (`lib/query-client.ts`), salvo los 401 y las consultas o mutaciones con `meta.silentError`
  (los formularios muestran el error junto a los campos). No se reintentan los errores 4xx.
- **Empresa y año:** `WorkspaceProvider`. El Admin elige la empresa en la topbar (se guarda en
  `opsynex.company`) y el cliente HTTP la envía como `X-Company-Id`. Analyst y Client usan la
  suya. Los años salen de `GET /kpis`; por defecto se usa el más reciente.
- **Formularios:** React Hook Form + Zod con las mismas reglas que el API (por ejemplo, la
  contraseña). Los errores de validación del API (`errors`) se asignan a sus campos.
- **Preferencias locales:** tema, menú colapsado y empresa elegida (en `localStorage`).

### Layout

- Sidebar oscura de 256 px, colapsable a 72 px (solo iconos con tooltip). El ítem activo lleva
  fondo `--sidebar-active` y barra izquierda de 3 px.
- Topbar de 64 px con el título de la sección, el selector de empresa (Admin), el de año, el tema
  y el menú de usuario (nombre, correo, rol, cerrar sesión).
- Debajo de 1024 px la sidebar es un drawer (`Sheet`) que se abre con el botón de menú. Debajo de
  768 px los selectores de empresa y año pasan a una segunda fila de la topbar.
- Contenido de hasta 1440 px con márgenes de 16 px en móvil y 32 px en escritorio. Verificado a
  375 px sin scroll horizontal (las tablas tienen scroll propio).

Capturas de referencia: `docs/capturas/etapa-08/` y `docs/documentacion/img/`.

### Carga de datos y homologación (Etapa 09)

- **`/uploads`** tiene tres pestañas (la pestaña activa queda en la URL: `?tab=historial|datos`):
  - **Nueva carga** — `UploadWizard`, un asistente de 3 pasos con `Stepper`:
    1. `FileDropzone`: arrastrar y soltar, con validación de extensión y tamaño en el cliente
       (`lib/upload-rules.ts`) y barra de progreso.
    2. `SheetList`: hojas detectadas, con las ignoradas atenuadas y su motivo.
    3. Resultado:
       - `IssueList`: errores bloqueantes o advertencias con hoja, fila y columna.
       - `AnalysisSummaryCard`: score, años, KPIs, problemas e impacto.
       - `ReviewNotice`: aviso de revisión recomendada.

    El estado del asistente se conserva al cambiar de pestaña.
  - **Historial** (`UploadHistory`): tabla de cargas con estado, revisión recomendada, fecha,
    usuario y filas. `ReportDialog` muestra el reporte de cada carga.
  - **Datos consolidados** (`ConsolidatedData`): `FinancialData` y datos operacionales del año
    de la topbar, en tablas de solo lectura con `tabular-nums`.
- **Subida con progreso:** `api/uploads.ts` usa `XMLHttpRequest`, porque fetch no informa el
  avance del envío. El 201 y el 422 traen el reporte; un 422 se muestra como error del archivo,
  no como fallo de red.
- **Resumen del análisis:** `GET /analysis/summary` (nuevo en el API). Al terminar una carga se
  invalidan todas las consultas para que topbar, KPIs y problemas se actualicen.
- **`/mappings`** (`MappingsPage` + `MappingReviewTable`):
  - **Elección de la carga:** por `?uploadId=`, o la última que necesita revisión.
  - **Filtro y búsqueda:** el filtro inicial es «Solo dudosas»; también se puede buscar.
  - **Categoría:** un `Select` por fila, agrupado por estado financiero. El catálogo viene de
    `GET /mappings/categories`.
  - **Confianza:** `lib/confidence.ts`; Alta ≥ 0,8, Media ≥ 0,5 y Baja por debajo, siempre con
    icono y palabra.
  - **Método:** traducido con `METHOD_LABELS`.
  - **Cambios:** se guardan localmente hasta pulsar «Guardar y volver a analizar». Ese botón
    hace `PUT /mappings` (confirma) y `POST /mappings/consolidate`, y muestra el resumen antes y
    después («Score 64,0 → 67,0», «Cuentas dudosas 2 → 0»).

### Dashboard, problemas y KPIs (Etapa 10)

- **`/` (Dashboard)** sigue el orden de la guía (§5):
  - `ScoreCard`: medidor, variación en puntos y componentes con su peso.
  - `ImpactCard`: total y barras horizontales por categoría.
  - `KpiCards`: 5 `StatCard` con `TrendIndicator`. Las proporciones varían en pp y el resto en %.
  - `TrendCharts`: una línea por KPI con selector, y barras de ingresos frente al costo total.
  - `TopProblems` y `Recommendations`, con los hallazgos positivos.

  Hace una consulta por bloque (`/kpis`, `/score`, `/impact/summary`, `/problems?pageSize=5`,
  `/data?type=Financial`), cada una con su skeleton. Si no hay datos, muestra un `EmptyState`.
- **`/problems`:**
  - Filtros en la URL (año, severidad, categoría y estado); el año por defecto es el de la topbar.
  - Tabla con orden por prioridad, columnas ordenables (incluido el impacto) y paginación.
- **`/problems/:id`:**
  - Descripción, severidad, estado (editable por Admin y Analyst) e impacto con «Cómo se
    calculó» (`evidence.impact`).
  - Histórico del KPI relacionado y evidencia legible (`lib/evidence.ts`).
  - Recomendación, más la de la IA cuando exista.
- **`/kpis`:** tabla histórica de todos los KPIs con la variación del último año, y un gráfico
  por KPI.
- **Gráficos** (`components/charts/`) sobre Recharts 3:
  - `ChartCard` aporta título, subtítulo con unidad y período, `aria-label` y «Ver tabla».
  - `TrendLineChart`, `GroupedBarChart` y `HorizontalBarChart`.
  - `ChartTooltip` y `ChartLegend` usan tokens; la leyenda aparece con 2 series o más.
  - Colores `var(--chart-N)` en orden fijo por entidad (ingresos = 1, costo = 2), sin doble eje
    Y y con grilla solo horizontal.
- **Montos:** `formatCurrency` antepone el símbolo de la moneda («RD$ 4.397.941,56», compacto
  «RD$ 4,4 M»), igual que el PDF.
- **Informe PDF:** `DownloadReportButton` llama a `GET /reports/executive` con el token y guarda
  el archivo con el nombre que envía el API (`Content-Disposition`, expuesto por CORS).
