## Pruebas y calidad

### Estrategia

| Nivel | Proyecto / herramienta | Qué cubre |
|---|---|---|
| Unitarias | `Opsynex.UnitTests` (xUnit v3 + Shouldly) | Motores puros (KPIs, reglas, impacto, score, consolidación), parsers, validadores, configuración de EF y arquitectura |
| Integración | `Opsynex.IntegrationTests` (`WebApplicationFactory` + SQL Server con Testcontainers) | Cada endpoint con su base de datos real: permisos, aislamiento por empresa, carga → homologación → análisis |
| Servicio de IA | `ai-service/tests` (pytest + httpx) | Endpoints, clave interna y motor de homologación |
| Frontend | Vitest + Testing Library | Páginas, componentes, formatos y reglas (etapas 08–12) |
| E2E | Playwright + axe-core (`e2e/`) | Flujo completo, permisos del Client, accesibilidad, teclado y móvil (etapa 13) |

Reglas: cada endpoint y cada motor nuevo lleva pruebas; las pruebas usan solo **libros
sintéticos** generados por script (nunca datos de clientes). La CI ejecuta `dotnet build -c
Release` (warnings = errores), `dotnet test` con cobertura mínima, `pytest` con cobertura mínima,
`ruff`, lint, tests y build del frontend, las tres auditorías de dependencias y los E2E.

### Cobertura actual (Etapa 07)

Al cierre del MVP (etapa 10): 540 pruebas .NET (350 unitarias y 190 de integración), 69 de
frontend y las de Python del servicio de IA. La tabla siguiente es la medición de la etapa 07.
Cobertura de líneas del backend, sin código generado ni migraciones:

| Proyecto | Cobertura |
|---|---|
| Opsynex.Domain | 96,3 % |
| Opsynex.Application | 97,1 % |
| Opsynex.Infrastructure | 91,7 % |
| Opsynex.Api | 88,0 % |
| **Total** | **94,2 %** |

Para medirla: `cd backend && dotnet test --collect:"XPlat Code Coverage"` (genera un
`coverage.cobertura.xml` por proyecto de pruebas en `TestResults/`).

### Base de datos en las pruebas (Etapa 01)

Las pruebas de integración (`Opsynex.IntegrationTests`) comparten una base de datos por
ejecución (`DatabaseFixture`):

- **Con Docker** (local, CI y nube con `setup_cloud.sh --with-sql`): levanta **SQL Server 2022
  real** con Testcontainers y aplica las migraciones. Es el modo por defecto.
- **Sin Docker:** usa SQLite in-memory con el esquema creado desde el modelo. Las pruebas que
  dependen de SQL Server (precisión decimal) se marcan como omitidas.
- `OPSYNEX_TEST_DB=sqlserver` exige Docker; `OPSYNEX_TEST_DB=sqlite` fuerza SQLite.

Cubren: crear y leer por empresa y año, filtro global por empresa, restricciones únicas
(empresa+año, empresa+cuenta, correo), auditoría de fechas, columnas JSON, borrado en cascada
y restricciones CHECK. Las pruebas unitarias verifican la configuración del modelo (índices
únicos, precisión, enums como texto, columnas JSON, filtros) y que no haya cambios del modelo
sin migración.

### Autenticación y permisos (Etapa 02)

- `Auth/AuthEndpointsTests`: login (claims del JWT, sin contraseña en la respuesta, mismo
  error para correo inexistente y contraseña errónea), `/me`, tokens ausentes, mal formados,
  con otra firma, vencidos o de un usuario cuyo rol cambió, y `register` por rol.
- `Auth/LockoutTests`: bloqueo al 5.º intento, `Retry-After`, desbloqueo por tiempo y por
  cambio de contraseña, expiración del token. Usa `TimeTravelApiFactory` con un
  `FakeTimeProvider` para adelantar el reloj sin esperas.
- `Auth/CompaniesEndpointsTests` y `Auth/UsersEndpointsTests`: CRUD, validaciones,
  conflictos y aislamiento por empresa (otra empresa → 404; selección de empresa del Admin).
- `Auth/PermissionMatrixTests`: cada política y endpoint real contra anónimo, Admin, Analyst y
  Client. Usa un controlador solo de pruebas (`TestPolicyController`) por política, porque los
  endpoints de carga y análisis llegan en etapas posteriores.

### Carga y validación de datos (Etapa 03)

Todas las pruebas usan los **libros sintéticos** de `backend/sample-data/` (nunca datos reales).
Para regenerarlos se necesita LibreOffice:

```bash
python3 backend/tests/TestData/generate_sample_workbooks.py
```

- `Parsing/WorkbookParserTests` (unitarias): clasificación de las 20 hojas, valores en bruto
  contra `expected_values.json` (ventas netas, descuentos, costo de ventas, total de activos,
  total de capital, energía de las notas…), nómina sin datos personales, serie mensual, balance
  de comprobación, cada inconsistencia de la sección 5 con su hoja y fila, archivos inválidos y
  plantillas.
- `Parsing/ParsingComponentTests` (unitarias): normalización de etiquetas, lectura de números
  contables, filtro de datos sensibles, clasificador, extractor, tolerancia e ISO 4217.
- `Uploads/UploadsEndpointsTests` y `DataEndpointsTests` (integración): carga del `.xls` y del
  `.xlsx`, valores guardados en la BD, privacidad (ningún nombre ni cédula en la BD ni en la
  respuesta), advertencias, 422 para archivos inválidos, 403 para el Client, 413 por tamaño,
  plantillas descargadas que el parser reconoce, y `/data` por tipo.

### Homologación (Etapa 04)

- **Python** (`ai-service/tests/test_homologation_*.py`):
  - Los 4 ejemplos del PDF.
  - Las 77 líneas del libro sintético con su categoría correcta y confianza ≥ 0,8 (100 %; el
    criterio pide ≥ 90 %).
  - Contexto (sección, tipo de hoja, código de cuenta), términos dominicanos, errores de tipeo,
    mapeos conocidos, cuentas desconocidas y el endpoint con su autenticación.
- **Unitarias .NET:** `ConsolidationEngineTests` (subtotales frente a detalles, notas frente a
  estados, depreciación, derivados) y la clave de cuenta, los nombres de categoría y el fallback.
- **Integración .NET:**
  - `HomologationEndToEndTests`: con el **servicio de IA real** (`AiServiceFixture` lo arranca
    con uvicorn). Cubre el `FinancialData` de los dos años contra los valores del generador (27
    campos), la ausencia de doble conteo, ≥ 90 % de mapeos correctos, "revisión recomendada" con
    cuentas desconocidas, reutilización de mapeos confirmados y corrección con nuevo análisis.
  - `HomologationStubTests`: cliente de IA simulado. Verifica que se aplican las sugerencias, que
    lo confirmado no se vuelve a pedir, los permisos y el 409.
  - `HomologationFallbackTests`: IA inalcanzable. El análisis sigue con el fallback y el health
    queda en `Degraded`.

Sin Python, las pruebas con IA real se omiten con su motivo. En la CI se exigen
(`OPSYNEX_REQUIRE_AI=true`) y el servicio se instala en el job de .NET.

### Motor de KPIs (Etapa 05)

- **Unitarias** (`Kpis/`):
  - Una `[Theory]` por fórmula, con casos de cero, null y negativos.
  - Tendencias: dirección, si es favorable, umbral configurable y crecimiento.
  - Normalización: lineal, con topes y con rangos invertidos.
  - Serie mensual: huecos y cambio de año.
  - Benchmarks de `appsettings.json` válidos para las dos industrias y las monedas.
- **Integración** (`Analysis/KpisEndpointsTests`):
  - Los 14 KPIs de cada año de los libros de 2 y 3 años coinciden con los calculados a mano por
    el generador (`expected_values.json → kpis`).
  - Tendencias, normalización en RD$, serie mensual, filtro por año, JSON guardado, recálculo con
    `/analysis/run` y permisos.

### Motor de diagnóstico (Etapa 06)

- **Unitarias** (`Diagnosis/DiagnosisRuleTests`):
  - Cada una de las 13 reglas con casos que la disparan (y su severidad) y que no la disparan
    (estable, mejora, falta de datos o del año anterior).
  - Umbrales configurables y reglas desactivadas.
  - Descubrimiento de todas las reglas.
  - Priorización y hallazgos positivos.
- **Integración** (`Analysis/ProblemsEndpointsTests`):
  - El libro de 3 años detecta `EBITDA_DECLINE`, `GROSS_MARGIN_DECLINE`,
    `OVERHEAD_OUTPACES_REVENUE` y `LABOR_COST_OUTPACES_REVENUE` con la severidad esperada.
  - Analizar dos veces (y subir de nuevo) no duplica problemas y conserva el estado.
  - Filtros, paginado, detalle con evidencia e historial del KPI, permisos y aislamiento por
    empresa.

### Impacto financiero y score (Etapa 07)

- **Unitarias** (`Diagnosis/ImpactEngineTests`):
  - Cada fórmula de impacto con un ejemplo numérico: Margin Loss (caída de margen, brecha con la
    referencia, caída de ventas), Excess Cost (crecimiento frente a ingresos y exceso sobre la
    referencia), Productivity Loss y Operational Waste (mermas y energía).
  - Alertas financieras sin pérdida pero con magnitud.
  - Datos faltantes → `null` con motivo; las fórmulas nunca devuelven negativos.
  - Frase del impacto con la moneda de la empresa.
- **Unitarias** (`Diagnosis/ScoreEngineTests`):
  - Pesos de la sección 13 (30/25/25/20) y redistribución proporcional cuando falta un componente.
  - Bandas de interpretación (80, 79,99, 60, 59,99), límites 0–100 y sin datos → sin score.
  - Tendencia de productividad, volatilidad del margen y validación de la configuración.
- **Integración** (`Analysis/ImpactAndScoreEndpointsTests`), con los libros de 2 y 3 años:
  - Punta a punta: subir → homologar → consolidar → `GET /kpis`, `/problems`, `/score` e
    `/impact/summary` coherentes entre sí.
  - Cada problema tiene un impacto mayor que 0 y el ranking dentro de cada severidad es por impacto.
  - El score de cada año está entre 0 y 100 y los pesos suman 1, con redistribución en los años
    sin empleados.
  - El total del resumen es igual a la suma de los problemas y a la suma por categoría.
  - El impacto de `EBITDA_DECLINE` coincide con los KPIs y los ingresos.
  - Errores (`company_required`, `invalid_year`), empresa sin datos y re-análisis sin duplicar scores.

### Frontend: carga y homologación (Etapa 09)

- **`UploadWizard.test.tsx`:**
  - Valida la extensión en el cliente sin subir nada.
  - Recorre el flujo completo: hojas detectadas (con las ignoradas y su motivo), resumen del
    análisis, advertencias con su ubicación y enlace al dashboard.
  - Muestra el aviso de revisión recomendada con el enlace a la carga.
  - Un archivo con errores muestra los mensajes, no permite ir al dashboard y deja subir otro.
- **`MappingReviewTable.test.tsx`:** cuenta con hoja, fila y sección; confianza Alta, Media o
  Baja; método; cambio de categoría; marca de cambios sin guardar; cuentas confirmadas.
- **`MappingsPage.test.tsx`:**
  - Muestra solo las dudosas al inicio, con el guardado deshabilitado sin cambios.
  - «Confirmar todas», luego guardar y volver a analizar (verifica el `PUT` y el resumen
    «Score 64,0 → 67,0» y «Cuentas dudosas 2 → 0»).
- **`upload-rules.test.ts`:** extensiones, archivo vacío y tamaño máximo; formatos de tamaño y
  de años.
- **Backend** (`Analysis/AnalysisSummaryEndpointsTests`): `/analysis/summary` coherente con
  `/score`, `/problems` e `/impact/summary`; catálogo de categorías; usuario de cada carga.
- **Manual con Playwright contra el API real:**
  - libro de 3 años → resumen → dashboard sin pasos intermedios;
  - variante con cuentas desconocidas → aviso → corrección → resumen actualizado;
  - archivo sin estados → errores sin continuar;
  - 375 px en modo oscuro sin scroll horizontal.

### Dashboard, problemas, KPIs e informe PDF (Etapa 10)

- **Frontend:**
  - `DashboardPage.test.tsx`:
    - Muestra score (medidor, variación y componentes), impacto (total con «RD$» y desglose),
      KPIs con su tendencia, tendencias, top de problemas con enlace al detalle, recomendaciones,
      «Lo que mejoró» y el botón del PDF.
    - Sin datos, muestra el estado vacío con «Subir archivo».
  - `ProblemsPage.test.tsx`:
    - La lista muestra severidad, impacto y estado, y el filtro de severidad llega a la consulta.
    - El detalle muestra el cálculo del impacto, el histórico, la evidencia y la recomendación.
    - El Analyst cambia el estado (`PATCH`); el Client no puede cambiarlo, pero descarga el PDF
      con su token.
    - Un problema inexistente muestra un estado vacío.
  - `KpisPage.test.tsx`: tabla histórica con variación (la dirección y si es favorable),
    gráfico por KPI con `aria-label` y «Ver tabla».
- **Backend:**
  - `Analysis/ReportsEndpointsTests`:
    - El PDF se genera para Client y Analyst con más de una página.
    - El texto, extraído con PdfPig, contiene la portada, el score y su interpretación, el
      impacto, los KPIs, la tendencia, los títulos de los problemas, las recomendaciones, la
      calidad de los datos, la nota metodológica y el pie con el número de página.
    - Funciona para un año anterior.
    - Errores: `reports.no_data`, `reports.company_required` y 401.
  - `ReportFormattingTests`: montos con «RD$», variaciones en pp o %, eje del gráfico, SVG de
    tendencia y nombre de archivo seguro.
- **Manual con Playwright contra el API real:**
  - dashboard, lista, detalle y KPIs en escritorio (claro y oscuro) y a 375 px sin scroll
    horizontal;
  - descarga del PDF como Admin, Analyst y Client;
  - cambio de estado de un problema.

  Capturas en `docs/capturas/etapa-10/`.

### Inteligencia artificial (Etapa 11)

Ninguna prueba llama a la API real de Claude: el servicio de IA usa un SDK simulado (`FakeSdk`) o
un cliente falso (`FakeLlm`), y el API .NET un `FakeAiInsightsClient`.

- **Servicio de IA (pytest, 134 pruebas):**
  - `test_llm_client.py` verifica los parámetros que se envían:
    - `output_format` Pydantic y `output_config.effort`;
    - `system` con `cache_control`;
    - `fallbacks="default"` y su beta, sin `thinking`.
  - También cubre `stop_reason` (`refusal` con categoría, `max_tokens`, salida inválida), la
    traducción de cada error del SDK (429, 5xx, 401, 404, 400, conexión, timeout), el registro de
    tokens y el bloqueo de datos personales antes de llamar al SDK.
  - `test_insights_api.py`: recomendación con causas y acciones; resumen recortado a 5 párrafos y
    3 prioridades; 503 `llm_not_configured` sin clave; errores como ProblemDetails; clave
    interna; validación.
  - `test_anomalies.py`: series sintéticas con un pico y una caída, pico estacional bajo el umbral
    de materialidad, serie corta, MAD = 0 y explicación en el endpoint.
  - `test_privacy.py`: patrones de datos personales y prompts construidos solo con agregados.
- **API .NET:**
  - `Analysis/AiInsightsEndpointsTests` (modo Inline con cliente simulado):
    - resumen del año y recomendaciones de los problemas Medium (la base se conserva);
    - caché por hash al volver a analizar, y «Regenerar» que fuerza la llamada;
    - anomalías como problemas Low que sobreviven al motor de reglas y se eliminan cuando ya no
      se detectan;
    - sin clave o con el servicio caído, el estado lo dice y KPIs, problemas y PDF siguen
      funcionando;
    - **payloads sin datos personales** con el libro sintético (que trae nómina);
    - el PDF incluye el resumen;
    - permisos.
  - `AiDisabledTests`: estado `disabled` y `regenerate` rechazado.
  - `AiInsightsClientTests`: JSON en snake_case, `llm_not_configured` como error propio y el resto
    como no disponible.
- **Frontend:** `ExecutiveSummaryCard.test.tsx` (listo, Client sin «Regenerar», generando, no
  configurada, no disponible) y `AiRecommendationPanel.test.tsx` (causas, acciones con esfuerzo y
  horizonte, y el estado vacío).
- **Manual contra el stack real sin clave de Claude:**
  - el servicio de IA recibe `/anomalies/detect` (200, sin anomalías por la materialidad) y
    `/executive-summary` (503 `llm_not_configured`);
  - el dashboard muestra «La IA generativa no está configurada…» y el resto del MVP funciona.

**Pendiente:** la prueba con una `ANTHROPIC_API_KEY` real (calidad y coherencia de las
respuestas en español con el libro sintético).

### Checklist MVP

Verificación punto por punto de la definición de MVP de las dos especificaciones, con el libro
sintético `libro_sintetico_3a.xls`.

**ESPECIFICACIONES_DEL_SISTEMA, sección 20**

| Criterio | Cumple | Evidencia |
|---|---|---|
| Carga archivos financieros correctamente | ✅ | `Uploads/*` (libros válidos e inválidos), `UploadWizard.test.tsx`, capturas `img/carga-paso*.png` |
| Calcula KPIs automáticamente | ✅ | `Analysis/KpisEndpointsTests` (14 KPIs por año al subir), `KpisPage.test.tsx`, captura `img/kpis.png` |
| Genera diagnósticos financiero-operacionales | ✅ | `DiagnosisRuleTests` (13 reglas), `ProblemsEndpointsTests`, captura `img/problemas.png` |
| Estima impacto financiero | ✅ | `ImpactEngineTests`, `ImpactAndScoreEndpointsTests`, `DashboardPage.test.tsx`, captura `img/problema-detalle.png` |
| Muestra dashboards correctamente | ✅ | `DashboardPage.test.tsx`, capturas `capturas/etapa-10/dashboard-*.png` (escritorio y 375 px, claro y oscuro) |
| Genera score operacional-financiero | ✅ | `ScoreEngineTests`, `ImpactAndScoreEndpointsTests`, medidor del dashboard |

**PLATAFORMA_DE_ANALISIS, sección 14 y módulos obligatorios**

| Criterio | Cumple | Evidencia |
|---|---|---|
| Calcula KPIs automáticamente | ✅ | Igual que arriba (KPIs financieros y, desde la etapa 12, industriales: `IndustrialEndpointsTests`) |
| Detecta al menos 3 tipos de problemas | ✅ | El libro de 3 años detecta 4 tipos (`ProblemsEndpointsTests`) |
| Muestra impacto financiero | ✅ | Dashboard, detalle de problema y PDF |
| Tiene dashboard funcional | ✅ | `DashboardPage.test.tsx` y recorrido con Playwright |
| Autenticación de usuarios | ✅ | `Auth/*`, `AuthProvider.test.tsx`, `RoleGuard.test.tsx` |
| Carga de datos (Excel/CSV) | ✅ | `.xls`, `.xlsx` y `.csv` (`Uploads/*`, plantillas) |
| Motor de diagnóstico, de impacto, dashboard y vista de problemas | ✅ | Ver las filas anteriores y `ProblemsPage.test.tsx` |

**Decisiones de FLUJO_DE_USUARIO.md**

| Decisión | Cumple | Evidencia |
|---|---|---|
| El Analyst sube y el Client solo ve | ✅ | `RoleGuard.test.tsx`, `Sidebar.test.tsx` |
| Análisis automático; revisión de cuentas opcional | ✅ | `UploadWizard.test.tsx`, `MappingsPage.test.tsx` |
| Solo el Admin crea usuarios y empresas | ✅ | `Auth/PermissionMatrixTests`, sin registro en el login |
| Informe PDF obligatorio, para todos los roles | ✅ | `ReportsEndpointsTests` (Client y Analyst) y descarga con Playwright como Admin, Analyst y Client |

Comandos de verificación: `cd backend && dotnet test` (SQL Server con Testcontainers) y
`cd frontend && npm test && npm run lint && npm run build`.

Los E2E automatizados con Playwright se ejecutan en la CI desde la etapa 13 (ver más abajo).

### Módulo industrial (etapa 12)

| Capa | Pruebas |
|---|---|
| Fórmulas | `IndustrialKpiEngineTests`: disponibilidad, rendimiento, calidad, OEE, scrap, costo por unidad, paradas, capacidad, las tres pérdidas, protección contra división por cero y agregación por planta, línea, máquina, mes y año. |
| Reglas e impacto | `IndustrialDiagnosisEngineTests`: cada regla dispara y no dispara en el umbral, severidades, impacto sin doble conteo, umbrales configurables y falta de costos. |
| Score | `IndustrialScoreEngineTests`: pesos de la especificación, redistribución, normalización (también invertida) y validación de la configuración. |
| Lectura | `IndustrialTableExtractorTests`: detección por encabezados, filas inválidas con su ubicación, varias tablas en un libro y plantillas generadas que se vuelven a leer. |
| API | `IndustrialEndpointsTests`: con `sample-data/industrial/` se detectan los 3 problemas de la especificación (y el de costo), con impactos iguales a los del generador (±0,1 %). También KPIs, filtros, score, permisos y estado de los problemas. `AiInsightsEndpointsTests`: series diarias y anomalías. |
| Servicio de IA | `test_anomalies.py`: series diarias de OEE y paradas. |
| Frontend | `ProductionPage`, `CostsPage`, `Sidebar` (menú habilitado con datos), `DashboardPage` (score industrial), `UploadWizard` (archivo industrial) y `format` (`formatDay`, `formatMonth`, `formatMinutes`). |

### Calidad, cobertura y E2E (etapa 13)

#### Cobertura

Medida combinando las pruebas unitarias y las de integración (`scripts/coverage_report.py` une
los reportes Cobertura de los dos proyectos). La CI falla por debajo del 80 % en Domain y
Application (.NET) y en el servicio de IA.

| Componente | Cobertura de líneas | Mínimo |
|---|---|---|
| Opsynex.Domain | 97,4 % | 80 % |
| Opsynex.Application | 96,8 % | 80 % |
| Opsynex.Infrastructure | 91,9 % | — |
| Servicio de IA (`app/`) | 98 % | 80 % |

`Opsynex.Api` sale más bajo en la medición combinada (33 %) porque incluye el código que generan
los source generators (logging, regex, OpenAPI) y el arranque. Sus endpoints están cubiertos por
las pruebas de integración y la matriz de seguridad.

```bash
cd backend && dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults/cov
python3 ../scripts/coverage_report.py TestResults/cov --min Opsynex.Domain=80 --min Opsynex.Application=80
cd ai-service && pytest --cov=app --cov-fail-under=80
```

#### Totales al cierre de la etapa 13

- **.NET:** 647 pruebas (418 unitarias y 229 de integración).
- **Servicio de IA:** 144 pruebas (pytest).
- **Frontend:** 88 pruebas (Vitest).
- **E2E:** 10 escenarios (Playwright).

#### E2E con Playwright (`e2e/`)

Se ejecutan contra la plataforma levantada (API, servicio de IA y frontend). `global-setup.ts`
crea por el API una empresa nueva con un Analyst y un Client, así las pruebas no dependen de
datos previos.

| Archivo | Escenario |
|---|---|
| `01-flujo-analista.spec.ts` | Login → carga de `libro_sintetico_3a.xls` → hojas detectadas → análisis → homologación → dashboard (score) → problemas → detalle (impacto, recomendación, cambio de estado). Luego los datos industriales habilitan Producción y Costos (MAQ-03 bajo el 60 %). |
| `02-permisos-client.spec.ts` | El Client no ve Carga, Homologación ni Administración, tampoco por URL directa; no puede cambiar el estado de un problema; sí descarga el PDF. |
| `03-accesibilidad.spec.ts` | axe-core (WCAG 2.1 A y AA) en login, dashboard, problemas, detalle, KPIs, carga, homologación, producción, costos y administración, en modo claro y oscuro y a 1440 y 375 px. |
| `04-teclado-y-movil.spec.ts` | Orden de tabulación y foco visible en el login, errores asociados con `aria-describedby`, «Saltar al contenido», `Esc` cierra los diálogos y devuelve el foco, y 375 px sin scroll horizontal. |

```bash
# Con la plataforma levantada (ver Instalación) y un Admin existente:
cd e2e && npm ci && npx playwright install chromium
E2E_ADMIN_EMAIL=admin@demo.opsynex.local E2E_ADMIN_PASSWORD='Demo2026!' npx playwright test
```

Variables: `E2E_BASE_URL` (frontend, `http://localhost:5173`), `E2E_API_URL`
(`http://localhost:5080`), `E2E_ADMIN_EMAIL` / `E2E_ADMIN_PASSWORD` y `PW_CHROMIUM_PATH` (un
Chromium ya instalado). En la CI, el job **E2E** levanta SQL Server como servicio, el servicio de
IA, el API en modo Production (migra y crea el Admin al arrancar) y el frontend compilado
(`vite preview`), y guarda el reporte si algo falla.

#### Accesibilidad: lo que se corrigió en la auditoría

- `--foreground-muted` no llegaba a 4,5:1. Pasó a `#6E6C66` en claro y `#9A9891` en oscuro.
- El menú de usuario y los selectores de empresa y año tenían un `aria-label` que no incluía el
  texto visible (WCAG 2.5.3). Ahora usan etiquetas reales.
- Las tablas que se desplazan a 375 px no se podían mover con el teclado. Ahora su contenedor es
  una región enfocable cuando desborda.
- `aria-describedby` apuntaba a una descripción inexistente en los formularios.
- Al cerrar con `Esc` un diálogo abierto por estado, el foco se perdía. Ahora vuelve al botón
  que lo abrió.
