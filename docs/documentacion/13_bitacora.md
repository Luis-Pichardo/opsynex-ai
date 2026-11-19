## Bitácora de desarrollo

Registro de cada etapa completada (la más reciente al final).

### Planificación del proyecto (15/11/2026)

- **Resumen:** análisis de las especificaciones del cliente, definición de 14 etapas,
  protocolo de trabajo con Claude Code y estructura de la documentación.
- **Archivos principales:** `CLAUDE.md`, `ESTADO.md`, `etapas/`, `docs/documentacion/`,
  `scripts/generar_documentacion.py`.
- **Decisiones:** núcleo del MVP sobre el modelo financiero anual; módulo industrial en la
  etapa 11; arquitectura multi-empresa; documentación Word generada desde Markdown.
- **Pendientes:** iniciar la Etapa 00.

### Cambio de stack y guía de diseño (15/11/2026)

- **Resumen:** se cambió el stack a C# / ASP.NET Core + SQL Server para el backend y Python
  para la inteligencia artificial; frontend con React + shadcn/ui. Se creó la guía de diseño
  de interfaz con tokens de color, tipografía y componentes.
- **Archivos principales:** `CLAUDE.md`, `etapas/` (15 etapas), `docs/diseno/`.
- **Decisiones:** servicio de IA separado y *stateless*; nueva etapa 11 de IA (recomendaciones
  y resumen ejecutivo con Claude, detección de anomalías); despliegue con la nube a elegir
  (Azure recomendada para .NET).
- **Pendientes:** iniciar la etapa 00.

### Etapa 00 — Preparación del entorno y estructura (15/11/2026)

- **Resumen:** se creó el monorepo con la solución .NET 10 en arquitectura limpia (API,
  Application, Domain, Infrastructure y dos proyectos de pruebas), el servicio de IA en
  Python/FastAPI con autenticación interna por cabecera y el frontend React + Vite + Tailwind +
  shadcn/ui con los tokens de diseño. Los tres tienen salud (`/health` o pantalla de estado),
  pruebas, linters y Dockerfile; hay `docker-compose.yml` y CI en GitHub Actions.
- **Archivos principales:** `backend/` (`Program.cs`, `Directory.*.props`, `Dockerfile`),
  `ai-service/app/` (`main.py`, `security.py`), `frontend/src/` (`components/ui/`,
  `components/health/`, `lib/format.ts`, `styles/tokens.css`), `tailwind.config.ts`,
  `docker-compose.yml`, `.env.example`, `.github/workflows/ci.yml`, `scripts/setup_cloud.sh`.
- **Decisiones técnicas:**
  - .NET 10 instalado desde el paquete de Ubuntu (`dotnet-sdk-10.0`, noble-updates); el
    repositorio de Microsoft para 24.04 no lo publica.
  - En la nube sí se puede arrancar `dockerd` y SQL Server 2022 en contenedor, así que las
    pruebas de integración con BD podrán usar Testcontainers (SQLite in-memory queda de respaldo).
  - xUnit v3 (`xunit.v3`) para tener `TestContext.Current.CancellationToken`.
  - OpenAPI versionado con `Asp.Versioning.OpenApi` (un documento por versión, `/openapi/v1.json`).
  - Serilog con logger inicial normal (no *bootstrap*), porque el *bootstrap logger* falla
    cuando `WebApplicationFactory` crea varios hosts en las pruebas.
  - Tailwind CSS 3.4 (no la 4) porque la guía de diseño pide mapear los tokens en
    `tailwind.config`; los componentes de shadcn/ui (estilo new-york) se copiaron a mano y se
    adaptaron a los tokens. El modo oscuro lo resuelven los tokens, sin variante `dark:`.
  - ESLint prohíbe `toLocaleString` en el frontend: todo formato pasa por `src/lib/format.ts`.
  - Versión del documento: 0.0.3 (la regla `0.N.0` daría 0.0.0, menor que la versión vigente).
- **Pendientes:** Etapa 01 (base de datos y modelos con EF Core, check de BD en `/health`).

### Etapa 01 — Base de datos y modelos (15/11/2026)

- **Resumen:** 10 entidades en `Opsynex.Domain/Entities` (Company, User, Upload,
  RawAccountValue, AccountMapping, FinancialData, OperationalData, KpiResult, Problem, Score),
  enums y catálogo `OpsynexCategory` en español. `OpsynexDbContext` con configuraciones Fluent
  API, convenciones de precisión, enums como texto, columnas JSON con `ISJSON`, índices por
  empresa y año y filtro global por empresa. Migración `InitialCreate`, migración automática en
  Development, check de BD en `/health`, repositorios, `IUnitOfWork` y respuesta 409 para
  duplicados.
- **Archivos principales:** `backend/src/Opsynex.Domain/` (Entities, Enums, Catalogs),
  `backend/src/Opsynex.Application/Abstractions/`, `backend/src/Opsynex.Infrastructure/Persistence/`,
  `backend/tests/Opsynex.IntegrationTests/Infrastructure/DatabaseFixture.cs`,
  `backend/tests/*/Persistence/`, `docs/documentacion/03_modelo_datos.md`.
- **Decisiones técnicas:**
  - Tablas en PascalCase plural (convención de SQL Server/EF) en lugar de `snake_case`.
  - Ids GUID v7 generados en la aplicación (ordenables, sin ida y vuelta a la BD).
  - Montos financieros y KPIs **nullable**: un dato no informado no se confunde con cero.
  - Sin borrado en cascada desde `Companies` (evita rutas múltiples de cascada en SQL Server
    y borrados accidentales); solo `Uploads → RawAccountValues`.
  - `Problem.Evidence` como `JsonObject` libre (lo usan las etapas 06 y 07);
    `Problem` único por (empresa, año, código) para re-ejecutar el diagnóstico sin duplicar.
  - Se agregó `ProblemCategory` (Profitability, CostEfficiency, Productivity,
    FinancialStability, Operations) y `ScoreInterpretation` (Healthy, Improvable, Deteriorated).
  - Filtro global con nombre (`Company`, función de EF Core 10) para poder ignorarlo
    selectivamente (login). Hasta la etapa 02 se usa `UnrestrictedTenant`.
  - Pruebas de integración con SQL Server real vía Testcontainers (funciona en la nube) y
    respaldo automático a SQLite in-memory sin Docker.
  - `dotnet-ef` como herramienta local (`backend/dotnet-tools.json`).
- **Pendientes:** Etapa 02 (autenticación JWT y roles; conectar `ICurrentTenant` al usuario).

### Etapa 02 — Autenticación y roles (18/11/2026)

- **Resumen:** login con JWT (HS256, claims `sub`, `email`, `role`, `company_id`), `GET /me`,
  alta de usuarios solo por Admin (`POST /auth/register` y `/users`), CRUD de empresas y
  usuarios, contraseñas con `PasswordHasher<User>`, bloqueo de 15 minutos tras 5 intentos
  fallidos, políticas `CanManage` / `CanAnalyze` / `CanView`, `ICurrentUser` y
  `HttpCurrentTenant` conectados al filtro global por empresa, seed del primer Admin (al
  arrancar o con `-- seed-admin`) y errores ProblemDetails con `code` (`auth.*`, `user.*`,
  `company.*`). Migración `AddUserLockout`.
- **Archivos principales:** `backend/src/Opsynex.Api/Auth/`, `Controllers/AuthController.cs`,
  `UsersController.cs`, `CompaniesController.cs`, `Extensions/AppExceptionHandler.cs`,
  `OpenApi/BearerSecuritySchemeTransformer.cs`, `backend/src/Opsynex.Application/{Auth,Users,Companies}/`,
  `Infrastructure/Security/PasswordService.cs`, `backend/tests/Opsynex.IntegrationTests/Auth/`.
- **Decisiones técnicas:**
  - **Sin auto-registro** (decisión del cliente en `FLUJO_DE_USUARIO.md`): `POST /auth/register`
    exige token de Admin; `POST /users` es equivalente.
  - Política por defecto "requiere sesión": todo endpoint nuevo queda protegido salvo que se
    marque `[AllowAnonymous]`. Efecto: sin token, una ruta inexistente responde 401 (no revela
    rutas).
  - En cada petición se verifica que el usuario siga activo y con el mismo rol y empresa: así
    desactivar a un usuario o cambiarle el rol corta su sesión sin esperar a que expire el token.
  - El bloqueo responde **423 Locked** con `Retry-After`; un correo inexistente responde igual
    que una contraseña errónea y se verifica un hash ficticio para igualar tiempos.
  - `DELETE /users/{id}` **desactiva** (sus cargas y confirmaciones lo referencian); un Admin
    no puede desactivarse ni quitarse el rol a sí mismo.
  - Los Analyst y Client pueden ver su propia empresa (`GET /companies`), lo que también
    demuestra el aislamiento (otra empresa → 404).
  - La expiración del JWT se valida con el `TimeProvider` de la aplicación (se puede probar
    adelantando el reloj).
  - SQLite (respaldo de pruebas) reporta `ON DELETE RESTRICT` con el código 1811; se traduce
    igual que el 547 de SQL Server a 409.
- **Pendientes:** cambio de contraseña por el propio usuario y recuperación por correo (no
  pedidos en el MVP); límites de tasa en el login (Etapa 13).

### Etapa 03 — Carga y validación de datos (18/11/2026)

- **Resumen:** ingesta del libro contable real de los clientes (`.xls` BIFF8 y `.xlsx`) y de
  plantillas simples (`.xlsx`/`.csv`): validación de firma y tamaño, clasificador de hojas por
  contenido, extractor de estados financieros sin posiciones fijas, extractores de nómina (solo
  conteos), ventas mensuales y balance de comprobación, y 9 validaciones de calidad como
  advertencias con hoja, fila y columna. Endpoints `POST/GET /uploads`, `GET /uploads/{id}`,
  plantillas `.xlsx` y `GET /data`. Generador de libros sintéticos con 19 archivos de prueba.
  Migración `IngestionModel` (columnas de origen en `RawAccountValue`, 13 campos en
  `FinancialData`, reporte JSON en `Uploads`, entidad `MonthlyFinancialData`).
- **Archivos principales:** `backend/src/Opsynex.Infrastructure/Parsing/`,
  `backend/src/Opsynex.Application/Uploads/`, `Controllers/UploadsController.cs`,
  `DataController.cs`, `backend/tests/TestData/generate_sample_workbooks.py`,
  `backend/sample-data/`, `backend/tests/*/Parsing|Uploads/`.
- **Decisiones técnicas:**
  - Se lee con `IExcelDataReader` fila por fila (sin `ExcelDataReader.DataSet`, que no hace
    falta). En .NET 10 `CodePagesEncodingProvider` viene incluido: no se necesita el paquete
    `System.Text.Encoding.CodePages`.
  - CsvHelper fijado en **33.1.0**: la 34 reescribió la API.
  - La firma se valida contra el contenido; un `.xls` que en realidad es `.xlsx` (o al revés) se
    lee igual con la advertencia `upload.extension_mismatch`.
  - Los subtotales y totales se guardan (`RowKind`) para validar y para los derivados de la
    etapa 04 (`TotalLiabilities = Total pasivos y capital − Total capital`), pero no se suman.
  - Tolerancia de cuadre = el mayor entre 1 unidad y 0,01 %.
  - Validación de subtotales con tres reglas (suma del grupo, subtotal anterior + grupo, suma de
    subtotales de la sección) y grupos cortados por los títulos, para no dar falsos avisos con
    líneas calculadas como "Ganancia bruta" o "Menos: depreciación acumulada".
  - Un archivo con errores bloqueantes se **registra** como `Failed` (aparece en el historial) y
    responde 422 con el mismo reporte; el archivo original nunca se guarda.
  - Privacidad: la nómina solo produce conteos y sumas por departamento; las etiquetas pasan por
    un filtro que reemplaza números de cuenta, cédulas y RNC por `***`; el nombre de la empresa
    del encabezado solo se compara (el mensaje nombra las hojas, no la empresa).
  - Monedas validadas contra una lista fija de ISO 4217 (no depende de la configuración regional).
  - Se agregó `UploadFileType.AccountingWorkbook` y los tipos de hoja `CashFlow`,
    `CostStructure` y `Operational` para las plantillas.
  - Bloqueante adicional `upload.file_type_required`: un CSV sin título no se puede clasificar.
- **Pendientes:** homologación y consolidación en `FinancialData` (Etapa 04); `PRESTLAB` e
  `IMPORT` (fase 2).

### Etapa 04 — Homologación contable y servicio de IA (18/11/2026)

- **Resumen:**
  - **Servicio de IA:** motor de homologación (`app/engines/homologation`) con un diccionario en
    español e inglés y términos dominicanos, contexto por hoja, sección y código de cuenta,
    palabras clave con prioridad y coincidencia difusa (rapidfuzz). Endpoint
    `POST /homologation/suggest`.
  - **API:** cliente tipado con resiliencia (timeout, reintentos y circuit breaker) y health check
    `Degraded`; fallback local; homologación y consolidación automáticas al subir.
  - **Datos:** motor puro `ConsolidationEngine` sin doble conteo, `MappingsController`, revisión
    recomendada con advertencias de cuentas sin categoría. Migración `Homologation`.
  - **Generador:** nueva variante `inc_cuentas_desconocidas`.
- **Archivos principales:** `ai-service/app/engines/homologation/`,
  `ai-service/app/routers/homologation.py`, `backend/src/Opsynex.Domain/Engines/Consolidation/`,
  `backend/src/Opsynex.Application/Homologation/`, `backend/src/Opsynex.Infrastructure/AiService/`,
  `Controllers/MappingsController.cs`, `backend/tests/Opsynex.IntegrationTests/Homologation/`.
- **Decisiones técnicas:**
  - El rol del mapeo (`Primary` o `Breakdown`) lo decide la hoja, no la IA: estados → Primary,
    notas → Breakdown. El balance de comprobación no se homologa (solo valida), para no sumarlo
    con los estados.
  - Clave de la cuenta = alcance + sección (solo subtotales de los estados) + etiqueta. Índice
    único `(CompanyId, Scope, Section, ClientAccount)` en `AccountMappings`.
  - Regla de consolidación "subtotales si existen, si no detalles", por categoría y rol: resuelve
    a la vez el capital (detalles + total), la compensación al personal (líneas + total de la
    nota) y las notas que repiten líneas de los estados.
  - `Method` guarda el origen real (`Exact`, `Keyword`, `Fuzzy`, `Rule`, `Fallback`,
    `Unmatched`…). "Automático" es un mapeo **no confirmado** (`ConfirmedAt` nulo); el confirmado
    por el usuario es `Manual`. La etapa pedía `Method = Auto`; se prefirió conservar el detalle
    del método.
  - Nueva categoría `TotalLiabilitiesAndEquity` para derivar `TotalLiabilities`.
  - `TotalCost` y `Ebitda` siempre se derivan (no son categorías).
  - Las etiquetas que deben quedar en `Other` a propósito están en el diccionario, así no
    disparan la revisión recomendada.
  - El health check del servicio de IA usa `Degraded`: sin IA el estado general de `/health` es
    `Degraded` y responde 200.
  - Pruebas de punta a punta con el servicio de IA real arrancado desde las pruebas
    (`AiServiceFixture`). La CI instala el servicio en el job de .NET y las exige.
- **Pendientes:** `AnalysisPipeline` (KPIs, diagnóstico, impacto y score) desde la etapa 05; las
  alternativas de la IA no se guardan (se muestran solo la categoría sugerida y su confianza).

### Etapa 05 — Motor de KPIs (18/11/2026)

- **Resumen:**
  - **Motor puro** (`Domain/Engines/Kpis`): catálogo de 14 KPIs, `KpiEngine`, `TrendAnalyzer`,
    `KpiNormalizer` y `MonthlyAnalyzer`.
  - **Benchmarks:** por industria y por moneda en `appsettings.json`, con opciones tipadas
    `BenchmarkOptions`.
  - **Ejecución:** `AnalysisPipeline` recalcula todos los años al consolidar y con
    `POST /api/v1/analysis/run`. `GET /api/v1/kpis` devuelve la serie histórica o un año, con
    tendencias, normalización y la serie mensual.
  - **Migración `KpiEngine`:** 9 columnas nuevas en `KpiResults` y los JSON `Trends` y
    `Normalized`.
  - **Generador:** calcula los KPIs esperados en Python con `Decimal` y los documenta en el
    README de `sample-data`.
- **Archivos principales:** `backend/src/Opsynex.Domain/Engines/Kpis/`,
  `backend/src/Opsynex.Application/Analysis/`, `Controllers/KpisController.cs`,
  `AnalysisController.cs`, `backend/tests/*/Kpis|Analysis/`, `generate_sample_workbooks.py`.
- **Decisiones técnicas:**
  - Un denominador **0 o negativo** da `null` (ingresos, empleados, patrimonio…); los
    numeradores negativos se aceptan (márgenes negativos).
  - Dirección de la tendencia por variación **porcentual** (±1 % configurable). Si el año
    anterior vale 0, se usa el signo del cambio absoluto.
  - Las tendencias comparan con el año inmediatamente anterior; si falta, no hay tendencia.
  - Los KPIs en moneda solo toman benchmarks de su moneda (nunca de la industria). El costo por
    unidad no tiene benchmark.
  - Los enums de las columnas JSON se guardan como texto (`"Up"`, `"BalanceSheet"`); los números
    guardados antes se siguen leyendo.
  - Los KPIs de años que ya no tienen datos financieros se eliminan al recalcular.
  - `CompanyScope` centraliza la empresa de la petición (la del usuario o la que eligió el Admin).
- **Pendientes:** diagnóstico, impacto y score se agregan al `AnalysisPipeline` en las etapas 06 y
  07. Calibrar los benchmarks con el cliente.

### Etapa 06 — Motor de diagnóstico (19/11/2026)

- **Resumen:**
  - **Motor de reglas** en `Domain/Engines/Diagnosis`: `IDiagnosticRule`, `DiagnosisEngine` y
    13 reglas (las 5 de la sección 11 y 8 adicionales). Severidad por magnitud con umbrales en
    `appsettings.json` (`DiagnosisRules`).
  - **Integración:** priorización por severidad y luego impacto, y hallazgos positivos. En el
    `AnalysisPipeline`, reemplazo idempotente que conserva el estado de cada problema.
  - **API:** `GET /problems` (filtros y paginado), `GET /problems/{id}` (evidencia e historial del
    KPI) y `PATCH /problems/{id}`.
  - **Migración `Diagnosis`:** columna `KpiResults.Highlights`.
- **Archivos principales:** `backend/src/Opsynex.Domain/Engines/Diagnosis/`,
  `backend/src/Opsynex.Application/Analysis/ProblemService.cs`, `AnalysisPipeline.cs`,
  `Controllers/ProblemsController.cs`, `backend/tests/*/Diagnosis|Analysis/`.
- **Decisiones técnicas:**
  - Un solo esquema de umbrales para todas las reglas: una **magnitud** positiva comparada con
    `Low`/`Medium`/`High`, más una `Reference` opcional por industria. Así cualquier regla se
    ajusta igual desde la configuración.
  - Nueva `ImpactCategory.FinancialAlert` para liquidez y endeudamiento (sin pérdida
    cuantificable).
  - Los problemas que dejan de detectarse se **eliminan** al volver a analizar; los que
    siguen conservan el estado que puso el usuario.
  - Hallazgos positivos guardados por año en `KpiResults.Highlights` y devueltos con
    `/problems`.
  - Las reglas se registran por reflexión (todas las `IDiagnosticRule` del dominio). Una regla
    nueva solo necesita su clase, sus umbrales por defecto y sus tests.
  - Textos en español con cifras ("−3,05 pp", "22,8 %") generados por el motor.
  - Con el libro de 3 años los márgenes de diferencia son pequeños (por ejemplo, overhead 7,58 %
    frente a ingresos 7,41 %). Se detectan como Low, coherente con "recomendación de monitoreo".
- **Pendientes:** el impacto económico (etapa 07) completará `ImpactValue` y el segundo criterio
  de la priorización.

### Etapa 07 — Impacto financiero y score (19/11/2026)

- **Resumen:**
  - **Motor de impacto** (`Domain/Engines/Impact/ImpactEngine.cs`): pérdida estimada, margen
    afectado, magnitud del deterioro y severidad para cada problema. Una fórmula por categoría:
    Margin Loss, Excess Cost, Productivity Loss y Operational Waste. Se guarda en
    `Problems.ImpactValue` y `Evidence.impact`.
  - **Score Opsynex** (`Domain/Engines/Scoring/ScoreEngine.cs`): 4 componentes con pesos
    30/25/25/20, redistribución proporcional si falta uno, e interpretación.
  - **`AnalysisPipeline` completo:** KPIs → diagnóstico → impacto → score en un único
    `SaveChanges`.
  - **API:** `GET /score` (un año o la serie) y `GET /impact/summary` (total, por categoría y top 5).
  - **Recomendaciones base con cifras** y la frase del impacto en la moneda de la empresa.
  - **Migración `ImpactAndScore`:** dimensiones del score anulables y columna `Scores.Components`.
- **Archivos principales:** `backend/src/Opsynex.Domain/Engines/Impact/`, `Engines/Scoring/`,
  `Engines/Diagnosis/Rules/`, `Opsynex.Application/Analysis/AnalysisPipeline.cs`,
  `ScoreService.cs`, `ImpactService.cs`, `Controllers/ScoreController.cs`,
  `Controllers/ImpactController.cs`, `backend/tests/*/Diagnosis|Analysis/`.
- **Decisiones técnicas:**
  - El impacto se calcula **por regla** con la fórmula de su categoría (una regla contra una
    referencia valora solo el exceso sobre la referencia, que es la parte evitable).
  - Las alertas financieras (liquidez, endeudamiento) no se cuantifican como pérdida: guardan
    `null` con el motivo y la magnitud.
  - La pérdida total del resumen es la **suma** de las estimaciones; puede superponer efectos
    (por ejemplo, costo de personal y margen EBITDA) y se documenta como orden de magnitud.
  - Productividad = 70 % nivel de ingresos por empleado + 30 % tendencia (±10 % → 0/100).
    Estabilidad incluye la volatilidad del margen EBITDA (desviación de los últimos 3 años;
    5 pp → 0). El flujo de caja operativo no se usa porque el archivo no lo trae.
  - El score se guarda con el detalle de cada componente (insumos, peso base y usado, motivo
    si falta) para que el dashboard explique el cálculo.
  - Pesos y parámetros configurables en `appsettings.json` (`Score`), validados al arrancar.
  - `GET /score` responde siempre con una lista de años (con `year`, 0 o 1 elemento), igual que
    `/kpis`.
- **Pendientes:** calibrar con el cliente benchmarks, umbrales y pesos; decidir si el resumen
  debe descontar la superposición entre problemas; la IA enriquecerá las recomendaciones en la
  etapa 11.

### Etapa 08 — Frontend base y sistema de diseño (19/11/2026)

- **Resumen:**
  - **Tema y tokens:** `ThemeProvider` (claro, oscuro, sistema), tokens mapeados en Tailwind y una
    prueba que prohíbe colores hex, tamaños de fuente y sombras sueltos.
  - **Componentes:** shadcn/ui adaptados a la guía (Button con estado de carga, Input, Select,
    Label, Card, Badge con icono, Table y DataTable, Tabs, Dialog, Sheet, DropdownMenu, Tooltip,
    Skeleton, Toaster, Form) y propios (`StatCard`, `EmptyState`, `PageHeader`, `ScoreGauge`,
    `SeverityBadge`, `TrendIndicator`, `ConfirmDialog`). Página `/design` solo en desarrollo.
  - **Cliente de API** con token, empresa (`X-Company-Id`), 401 → cierre de sesión y
    ProblemDetails → toast.
  - **Autenticación:** login a pantalla dividida sin registro, `AuthProvider`, `ProtectedRoute`
    y `RoleGuard` (también por URL directa).
  - **Layout:** sidebar colapsable por rol, topbar con empresa, año, tema y usuario, y drawer en
    móvil.
  - **Administración:** empresas y usuarios (alta, edición, contraseña, desactivación, filtros).
  - Secciones de las etapas 09 y 10 con `EmptyState`.
- **Archivos principales:** `frontend/src/{api,auth,workspace,theme,router}/`,
  `frontend/src/components/{ui,common,layout,admin}/`, `frontend/src/pages/`,
  `docs/capturas/etapa-08/`.
- **Decisiones técnicas:**
  - React Router 7 (modo data router) con rutas diferidas para Administración y `/design`; el
    bundle se divide en chunks de librerías (react, ui, data) y el principal pesa ~41 kB.
  - Token en `localStorage` con cierre por vencimiento y por 401. Se descartaron las cookies
    httpOnly porque el API ya usa JWT Bearer y CORS sin credenciales.
  - Errores del API como toast persistente (los de error no se cierran solos); los formularios
    los muestran en sus campos.
  - `tailwind-merge` extendido con la escala tipográfica de la guía.
  - Nuevo token `--overlay` para el fondo de modales y drawers (agregado a la guía).
  - El Admin elige la empresa en la topbar; las demás pantallas la usan sin repetir el filtro.
  - Se retiró la tarjeta provisional de salud del API (etapa 00): el layout la reemplaza.
- **Pendientes:** pantallas de carga y homologación (etapa 09); dashboard, problemas y KPIs
  (etapa 10).

### Etapa 09 — Frontend: carga de datos y homologación (19/11/2026)

- **Resumen:**
  - **Asistente de carga** de 3 pasos: subir con validación y progreso → hojas detectadas →
    resultado con errores, advertencias y resumen del análisis automático. Muestra el aviso de
    revisión cuando hay cuentas dudosas.
  - **Homologación opcional:** filtro de dudosas, categoría editable, confianza y método,
    «Confirmar todas» y «Guardar y volver a analizar», con lo que cambió.
  - **Historial de cargas** con reporte y **datos consolidados** por año.
  - **API:** `GET /analysis/summary`, `GET /mappings/categories` y `uploadedBy` en el listado de
    cargas (con pruebas de integración).
- **Archivos principales:** `frontend/src/components/uploads/`, `components/mappings/`,
  `pages/UploadsPage.tsx`, `pages/MappingsPage.tsx`, `api/{uploads,mappings,analysis}.ts`,
  `backend/src/Opsynex.Application/Analysis/AnalysisSummaryService.cs`.
- **Decisiones técnicas:**
  - El resumen sale de un endpoint propio en vez de combinar cuatro llamadas en el cliente.
  - La subida usa `XMLHttpRequest` para mostrar el progreso real del envío. Cuando el envío
    llega al 100 %, la barra pasa a «Analizando…» mientras el API procesa.
  - El paso 2 (hojas) se muestra después del análisis y no exige ninguna acción sobre los
    datos: solo un clic para ver el resultado.
  - Los mapeos confirmados se recuerdan, así que la misma variante subida de nuevo a la misma
    empresa ya no pide revisión.
  - Los cambios de la homologación se guardan juntos (un `PUT` y un `consolidate`) para no
    analizar varias veces.
- **Pendientes:** dashboard, problemas y KPIs (etapa 10).

### Etapa 10 — Dashboard ejecutivo, problemas e informe PDF: cierre del MVP (19/11/2026)

- **Resumen:**
  - **Dashboard:** score (medidor, variación y componentes), impacto (total y por categoría),
    5 KPIs con tendencia, tendencias (línea por KPI y barras de ingresos frente a costo),
    principales problemas, recomendaciones y «Lo que mejoró».
  - **Problemas:** lista con filtros y detalle con impacto y su cálculo, histórico del KPI,
    evidencia, recomendación y cambio de estado.
  - **KPIs:** tabla histórica y gráfico por KPI. Todos los gráficos tienen tooltip, leyenda,
    `aria-label` y «Ver tabla».
  - **Informe PDF** en el backend con QuestPDF (`GET /reports/executive`) y botón de descarga
    para todos los roles.
  - **Checklist MVP** documentado con evidencia.
- **Archivos principales:**
  - `backend/src/Opsynex.Application/Reports/`, `backend/src/Opsynex.Infrastructure/Reports/`
    y `Controllers/ReportsController.cs`.
  - `frontend/src/components/{charts,dashboard}/` y
    `frontend/src/pages/{DashboardPage,ProblemsPage,ProblemDetailPage,KpisPage}.tsx`.
- **Decisiones técnicas:**
  - QuestPDF con licencia Community y la fuente Inter embebida (OFL). El gráfico del PDF es un
    SVG generado en C#, con los colores de la guía y sin doble eje.
  - El texto del PDF se verifica en las pruebas con PdfPig. PdfPig no extrae algunos signos
    (paréntesis, «−»), así que las pruebas buscan frases sin ellos.
  - `formatCurrency` antepone el símbolo de la moneda («RD$», «US$»), como la guía y el PDF.
  - CORS expone `Content-Disposition` (nombre del PDF) y `Retry-After`.
  - La variación del score se muestra en puntos («2,3 pts»), no en pp.
  - Recharts 3.10.1, con versión fija.
  - `setup_cloud.sh` elimina el `docker.pid` huérfano que deja un reinicio del contenedor.
- **Pendientes:** IA (etapa 11): recomendaciones enriquecidas, anomalías y resumen ejecutivo en
  el PDF. Módulo industrial (etapa 12). E2E automatizados en la CI (etapa 13).

### Etapa 11 — Inteligencia artificial: recomendaciones, anomalías y resumen ejecutivo (19/11/2026)

- **Resumen:**
  - **Servicio de IA:** cliente de Claude con el SDK oficial `anthropic` 2.x. Usa salida
    estructurada con Pydantic, revisa `stop_reason` (incluido `refusal`), tiene respaldo de
    servidor, errores por tipo, timeouts, registro de tokens y prompt caching del system prompt.
  - **Endpoints:** `/recommendations/generate`, `/executive-summary` y `/anomalies/detect`
    (z-score robusto con MAD y materialidad, o variaciones bruscas).
  - **API .NET:** paso opcional `Ai:Enabled` después del análisis, en segundo plano. Guarda las
    recomendaciones en `Problem.AiRecommendation`, la entidad `ExecutiveSummary` y los
    problemas `ANOMALY_*`, con caché por hash de la entrada. Endpoints `GET /ai/summary` y
    `POST /ai/regenerate`. El PDF incluye el resumen y las recomendaciones de IA.
  - **Frontend:** tarjeta «Resumen ejecutivo» y pestañas de recomendación base / IA.
- **Archivos principales:** `ai-service/app/llm/`, `ai-service/app/engines/anomalies.py`,
  `ai-service/app/routers/insights.py`, `backend/src/Opsynex.Application/Insights/`,
  `backend/src/Opsynex.Infrastructure/AiService/{AiInsightsClient,AiEnrichmentQueue}.cs`,
  `Controllers/AiController.cs`, `frontend/src/components/{dashboard/ExecutiveSummaryCard,problems/AiRecommendationPanel}.tsx`.
- **Decisiones técnicas:**
  - **Segundo plano:** el enriquecimiento usa una cola en memoria y un `BackgroundService` para
    que subir un archivo no espere a Claude. El estado vive en un `AiJobTracker` en memoria (una
    instancia del API); los resultados, en la base de datos.
  - **Materialidad en anomalías (25 %):** sin ella, diciembre +22 % del libro sintético salía
    como anomalía con z ≈ 4,2.
  - **Hash con decimales normalizados:** un valor calculado y el mismo leído de SQL Server
    tienen distinta escala (`181741000` frente a `181741000.00`) y regeneraban sin necesidad.
  - **Orden de las llamadas:** el resumen va primero. Si la IA no está configurada, falla ahí y
    no se hacen más llamadas.
  - **Respaldo ante rechazos:** `fallbacks="default"` está activado por defecto
    (`ANTHROPIC_FALLBACKS=none` lo apaga).
  - **Privacidad:** el servicio bloquea antes de llamar a Claude cualquier texto con forma de
    cédula, RNC, correo, teléfono o fila de nómina, además de la prueba de payloads del API.
  - **Entorno de la nube:** las claves de Claude no se pueden guardar como secretos de red, así
    que en esta sesión no hubo prueba con Claude real.
- **Pendientes:**
  - Validar con `ANTHROPIC_API_KEY` real la calidad de las recomendaciones y del resumen con el
    libro sintético (criterio 1 de la etapa).
  - Si se despliega más de una instancia del API, mover la cola y el estado de la IA a un
    almacenamiento compartido.


### Etapa 12 — Módulo industrial: producción, OEE, scrap y paradas (19/11/2026)

- **Resumen:**
  - **Modelo:** tablas `ProductionData`, `CostData`, `StandardData` e `IndustrialKpiResults`
    (migración `IndustrialModule`).
  - **Carga:** tipos `Production`, `Costs` y `Standards`. Se reconocen por sus encabezados, se
    validan fila por fila y tienen plantillas `.xlsx` con instrucciones.
  - **Motores puros** en `Domain/Engines/Industrial`:
    - KPIs (disponibilidad, rendimiento, calidad, OEE, scrap, costo por unidad, paradas y
      capacidad) por mes y año, para la planta, cada línea y cada máquina.
    - Reglas `LOW_OEE`, `HIGH_SCRAP`, `HIGH_DOWNTIME` y `COST_PER_UNIT_ABOVE_STANDARD`, con su impacto.
    - Score industrial (30/25/25/20).
  - **API:** `/industrial/kpis` y `/industrial/score`. La IA detecta anomalías en las series
    diarias de OEE y paradas.
  - **Frontend:** páginas Producción y Costos, menú habilitado con datos y score industrial en
    el dashboard.
  - **Datos de ejemplo** sintéticos en `backend/sample-data/industrial/`.
- **Archivos principales:**
  - `backend/src/Opsynex.Domain/Engines/Industrial/`
  - `backend/src/Opsynex.Application/Industrial/`
  - `backend/src/Opsynex.Application/Uploads/IndustrialTemplates.cs`
  - `backend/src/Opsynex.Infrastructure/Parsing/IndustrialTableExtractor.cs`
  - `Controllers/IndustrialController.cs`
  - `backend/tests/TestData/generate_industrial_samples.py`
  - `frontend/src/pages/{ProductionPage,CostsPage}.tsx` y `frontend/src/components/industrial/`
- **Decisiones técnicas:**
  - **Reglas por máquina:** se evalúan con los agregados anuales de cada máquina (es lo
    accionable). Cada regla da un problema por año que lista las máquinas afectadas, en la misma
    tabla `Problems`. El pipeline financiero ya no borra los códigos industriales.
  - **Sin doble conteo:** `LOW_OEE` suma la pérdida por velocidad más las paradas y el scrap que
    no estén en `HIGH_DOWNTIME` o `HIGH_SCRAP`. El costo sobre el estándar descuenta lo ya
    atribuido a la máquina. Así la suma de problemas en el dashboard no repite pérdidas.
  - **Umbral de paradas como proporción:** `HIGH_DOWNTIME` usa paradas / tiempo planificado
    > 15 %, configurable, y no minutos absolutos, que dependen del tamaño de la planta.
  - **Score industrial al consultar:** no se guarda, porque depende del Score Opsynex del año,
    que cambia con cada carga financiera.
  - **Costos por meses cubiertos:** el costo por unidad solo divide entre la producción de los
    meses que tienen costos.
  - **Valores esperados independientes:** el generador de datos calcula KPIs e impacto en Python.
    Las pruebas de integración comparan el motor .NET contra ellos.
  - **Privacidad:** solo se leen las columnas de la plantilla; nombres de operadores u otras
    columnas no se guardan.
- **Pendientes:**
  - **Pendiente de confirmar con el cliente:** el alcance del módulo industrial
    (FLUJO_DE_USUARIO.md, decisión 5).
  - Validar con datos reales de una planta los umbrales y rangos del score (son configurables).
  - El informe PDF todavía no incluye la sección industrial.

### Etapa 13 — Seguridad, calidad y datos de demostración (19/11/2026)

- **Resumen:**
  - **Seguridad:**
    - Límites de peticiones en el login y en las cargas.
    - Cabeceras de seguridad y CSP en el API y en el frontend (nginx), HSTS, cabeceras de proxy,
      `Cache-Control: no-store`.
    - Libros con macros rechazados; logs sin cuerpos ni correos.
    - Prueba que recorre **todos** los endpoints (roles y aislamiento por empresa).
    - Auditorías de dependencias limpias.
  - **Calidad:**
    - Correlation id de punta a punta (API → logs → servicio de IA).
    - Cobertura combinada de 97 % en Domain y Application, y 98 % en el servicio de IA, con
      mínimos en la CI.
  - **E2E:** Playwright + axe en la CI (flujo del Analyst, permisos del Client, accesibilidad,
    teclado y 375 px).
  - **Demo:** comando `seed-demo` (2 empresas ficticias, 5 usuarios y análisis completo en unos
    10 s).
  - **Rendimiento:** lecturas sin seguimiento de cambios, paginación de los listados y
    compresión.
  - **Accesibilidad:** contraste, nombres accesibles, tablas desplazables con teclado, foco al
    cerrar diálogos y `aria-describedby` válido.
- **Archivos principales:**
  - `backend/src/Opsynex.Api/Extensions/{SecurityExtensions,Paging,ReadOnlyRequestTrackingPolicy}.cs`
  - `backend/src/Opsynex.Api/Demo/DemoSeeder.cs`
  - `Application/Abstractions/Diagnostics/CorrelationContext.cs`
  - `Infrastructure/AiService/CorrelationIdHandler.cs`
  - `ai-service/app/correlation.py`
  - `tests/…/Auth/EndpointSecurityTests.cs`
  - `scripts/coverage_report.py`
  - `e2e/`
  - `frontend/nginx.conf.template`
  - `.github/workflows/ci.yml`
- **Decisiones técnicas:**
  - **Matriz de endpoints descubierta del host:** la prueba lee los endpoints registrados en
    vez de una lista manual, así un endpoint nuevo sin acceso definido hace fallar la CI.
  - **`AsNoTracking` por tipo de petición:** se aplica a GET/HEAD con una política inyectable,
    no en el repositorio, porque los mismos repositorios cargan y modifican entidades en las
    escrituras.
  - **Paginación con cabeceras en los listados que eran arreglos:** el frontend y los clientes
    existentes no cambian de contrato. El frontend pide `pageSize=500` y pagina en el cliente.
  - **`seed-demo` reutiliza `UploadService`** actuando como el Analyst de cada empresa (contexto
    HTTP simulado). Así la demo es exactamente lo que vería un usuario real. Es idempotente.
  - **Token `--foreground-muted`:** se oscureció en claro y se aclaró en oscuro para cumplir AA
    (4,5:1); la guía y `tokens.css` quedan alineados.
  - **E2E con empresa propia por ejecución:** `global-setup` la crea por el API, sin depender de
    la demo ni de datos previos.
- **Pendientes:**
  - Rotación de secretos y gestor de secretos de la nube (etapa 14).
  - La validación con una clave real de Claude (etapa 11) sigue pendiente.
