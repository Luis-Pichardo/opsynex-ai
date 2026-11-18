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
