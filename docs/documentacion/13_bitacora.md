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
