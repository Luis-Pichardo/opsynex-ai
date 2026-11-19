## API REST

API REST en ASP.NET Core con prefijo versionado `/api/v1`. En Development la documentación
interactiva está en `/scalar` (OpenAPI en `/openapi/v1.json`); para probar endpoints
protegidos, pega en Scalar el `accessToken` obtenido en `POST /api/v1/auth/login`.

### Convenciones

- **Autenticación:** cabecera `Authorization: Bearer <accessToken>`. Todos los endpoints
  exigen sesión salvo `POST /auth/login`, `GET /health`, `GET /api/v1/system/info` y la
  documentación. Sin sesión, incluso una ruta inexistente responde 401.
- **JSON:** nombres en camelCase; los enums viajan como texto (`"role": "Analyst"`); fechas en
  ISO 8601 UTC.
- **Empresa (multi-empresa):** Analyst y Client solo ven datos de su empresa; el recurso de
  otra empresa responde **404**. Un Admin ve todas las empresas, o solo una si envía la
  cabecera `X-Company-Id: <id>` o el parámetro `?companyId=<id>` (valor no válido → 400
  `tenant.invalid_company_id`).
- **Errores:** formato ProblemDetails (RFC 7807) con `code` estable y `traceId`. Los errores de
  validación incluyen `errors` por campo:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Datos no válidos",
  "status": 400,
  "detail": "Revisa los campos marcados.",
  "errors": { "password": ["La contraseña debe incluir al menos un número."] },
  "code": "validation_failed",
  "traceId": "00-…"
}
```

### Códigos de error

| HTTP | `code` | Cuándo |
|---|---|---|
| 400 | `validation_failed` | Datos de entrada no válidos (detalle en `errors`) |
| 400 | `user.company_not_found` | La empresa indicada para el usuario no existe |
| 400 | `tenant.invalid_company_id` | `X-Company-Id` / `companyId` no es un identificador válido |
| 401 | `auth.unauthorized` | Falta el token |
| 401 | `auth.invalid_token` | Token mal formado, con firma incorrecta o de otro emisor |
| 401 | `auth.token_expired` | El token expiró (por defecto a los 60 minutos) |
| 401 | `auth.session_invalid` | El usuario fue desactivado o cambió su rol o empresa: debe volver a iniciar sesión |
| 401 | `auth.invalid_credentials` | Correo o contraseña incorrectos (mismo mensaje en ambos casos) |
| 403 | `auth.forbidden` | El rol no tiene permiso para la acción |
| 403 | `auth.user_inactive` | Login de un usuario desactivado |
| 404 | `not_found` | El recurso no existe o pertenece a otra empresa |
| 409 | `user.email_taken` | Ya existe un usuario con ese correo |
| 409 | `user.self_modification` | Un Admin intenta desactivarse o quitarse el rol de Admin |
| 409 | `company.in_use` | La empresa tiene usuarios o datos y no se puede eliminar |
| 409 | `conflict` | Registro duplicado (restricción única) |
| 423 | `auth.locked_out` | Cuenta bloqueada tras 5 intentos fallidos (cabecera `Retry-After` en segundos) |
| 400 | `upload.file_required` | Falta el archivo en el campo `file` |
| 400 | `upload.unsupported_extension` | El archivo no es `.xls`, `.xlsx` ni `.csv` |
| 400 | `upload.company_required` / `upload.company_not_found` | El Admin no indicó la empresa o no existe |
| 400 | `data.company_required` | El Admin consulta `/data` sin elegir empresa |
| 400 | `score.company_required` / `impact.company_required` | El Admin consulta `/score` o `/impact/summary` sin elegir empresa |
| 400 | `score.invalid_year` / `impact.invalid_year` | `year` fuera de 1900–2200 |
| 413 | `upload.too_large` | El archivo supera `Upload:MaxMb` (20 MB) |
| 400 | `mapping.company_required` | El Admin consulta `/mappings` sin elegir empresa |
| 409 | `upload.failed` | Se intenta analizar una carga con errores bloqueantes |
| 422 | `upload.*` (en `errors[]` del reporte) | Error bloqueante del archivo: `empty_file`, `invalid_signature`, `unreadable`, `no_financial_statements`, `years_not_found`, `file_type_required` |
| 500 | `internal_error` | Error inesperado (se registra en el log con su `traceId`) |

### Roles y permisos

Políticas de autorización: `CanManage` (Admin), `CanAnalyze` (Admin y Analyst) y `CanView`
(todos los roles).

| Acción | Admin | Analyst | Client |
|---|---|---|---|
| Gestionar empresas y usuarios | ✅ | ❌ | ❌ |
| Subir archivos, descargar plantillas, revisar la homologación y volver a analizar | ✅ | ✅ | ❌ |
| Ver KPIs, problemas, score y dashboard | ✅ | ✅ | ✅ |

No existe auto-registro: **solo un Admin crea usuarios** (de cualquier rol).

### Autenticación — `/api/v1/auth`

| Método | Ruta | Rol | Request | Response | Errores |
|---|---|---|---|---|---|
| POST | `/auth/login` | Público | `{ email, password }` | 200 `LoginResponse` | 400, 401 `auth.invalid_credentials`, 403 `auth.user_inactive`, 423 `auth.locked_out` |
| POST | `/auth/register` | Admin | `CreateUserRequest` | 201 `UserDto` + `Location` | 400, 401, 403, 409 `user.email_taken` |
| GET | `/auth/me` | Todos | — | 200 `UserDto` | 401 |

`LoginResponse`:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs…",
  "tokenType": "Bearer",
  "expiresAt": "2026-11-18T15:26:23Z",
  "user": { "id": "…", "email": "ana@empresa.com", "fullName": "Ana", "role": "Analyst",
            "companyId": "…", "companyName": "Empresa Demo", "isActive": true,
            "lastLoginAt": "2026-11-18T14:26:23Z", "createdAt": "…" }
}
```

El JWT (HS256) lleva los claims `sub` (Id del usuario), `email`, `role`, `company_id` (solo
si el usuario tiene empresa), `iat`, `nbf`, `exp`, `iss` (`opsynex-api`) y `aud`
(`opsynex-web`). Nunca se devuelve la contraseña ni su hash.

### Usuarios — `/api/v1/users` (solo Admin)

| Método | Ruta | Request | Response | Errores |
|---|---|---|---|---|
| GET | `/users?companyId=&role=&search=` | — | 200 `UserDto[]` (ordenados por nombre) | 401, 403 |
| GET | `/users/{id}` | — | 200 `UserDto` | 404 |
| POST | `/users` | `CreateUserRequest` | 201 `UserDto` | 400, 409 `user.email_taken` |
| PUT | `/users/{id}` | `UpdateUserRequest` | 200 `UserDto` | 400, 404, 409 `user.email_taken` / `user.self_modification` |
| PUT | `/users/{id}/password` | `{ newPassword }` | 204 (también quita el bloqueo) | 400, 404 |
| DELETE | `/users/{id}` | — | 204 (desactiva, no borra) | 404, 409 `user.self_modification` |

- `CreateUserRequest`: `{ email, password, fullName, role, companyId }`. `companyId` es
  obligatorio para Analyst y Client y opcional para Admin (Admin global sin empresa).
- `UpdateUserRequest`: `{ email, fullName, role, companyId, isActive }`.
- Contraseña: mínimo 8 caracteres, con al menos una letra y un número (máximo 128).

### Empresas — `/api/v1/companies`

| Método | Ruta | Rol | Request | Response | Errores |
|---|---|---|---|---|---|
| GET | `/companies?search=` | Todos (Analyst/Client: solo la suya) | — | 200 `CompanyDto[]` | 401 |
| GET | `/companies/{id}` | Todos (otra empresa → 404) | — | 200 `CompanyDto` | 404 |
| POST | `/companies` | Admin | `CompanyRequest` | 201 `CompanyDto` | 400, 403 |
| PUT | `/companies/{id}` | Admin | `CompanyRequest` | 200 `CompanyDto` | 400, 403, 404 |
| DELETE | `/companies/{id}` | Admin | — | 204 | 403, 404, 409 `company.in_use` |

- `CompanyRequest`: `{ name, industry: "Manufacturing" | "Services", sector?, currency? }`;
  `currency` es un código ISO 4217 de 3 letras en mayúsculas (por defecto `USD`; p. ej. `DOP`).
- `CompanyDto`: `{ id, name, industry, sector, currency, createdAt, updatedAt }`.

### Cargas — `/api/v1/uploads`

| Método | Ruta | Rol | Request | Response | Errores |
|---|---|---|---|---|---|
| POST | `/uploads` | Admin, Analyst | multipart: `file` (obligatorio), `companyId` (solo Admin), `fileType` (opcional; necesario para un CSV sin título) | 201 `UploadReportDto` + `Location` · 422 `UploadReportDto` con `status: "Failed"` | 400, 401, 403, 413 |
| GET | `/uploads` | Todos | — | 200 `UploadListItemDto[]` (más reciente primero) | 401 |
| GET | `/uploads/{id}` | Todos (otra empresa → 404) | — | 200 `UploadReportDto` | 404 |
| GET | `/uploads/templates/{fileType}` | Admin, Analyst | `fileType`: `IncomeStatement`, `BalanceSheet`, `CashFlow`, `CostStructure` u `Operational` | 200 archivo `.xlsx` con los dos últimos años cerrados | 403, 404 |

`UploadReportDto`:

```json
{
  "uploadId": "…", "companyId": "…", "fileName": "estados_2024.xls",
  "fileType": "AccountingWorkbook", "status": "Validated", "years": [2023, 2024],
  "sheets": [
    { "name": "BALANCE", "detectedType": "BalanceSheet", "rowsExtracted": 24, "ignored": false, "reason": null },
    { "name": "REGPASCUAL", "detectedType": "EmployeeRoster", "rowsExtracted": 15, "ignored": false,
      "reason": "Solo se guarda la cantidad de empleados." },
    { "name": "ANEXOA1", "detectedType": "Unknown", "rowsExtracted": 0, "ignored": true,
      "reason": "No se reconoce el contenido (hoja fiscal o de trabajo); se ignora." }
  ],
  "errors": [],
  "warnings": [
    { "code": "quality.note_mismatch", "message": "2024: la nota 4 suma … y no cuadra con «Cuentas por Cobrar» …",
      "sheet": "nota1", "row": 18, "column": "G" }
  ],
  "summary": { "employeeCount": 15, "employeesByDepartment": [{ "department": "Produccion", "employees": 8, "payroll": 3048000.0 }, …],
               "annualPayroll": 7598000.0, "monthlyRecords": 12, "trialBalanceReliable": true, "trialBalanceDifference": 0 },
  "rowsCount": 412,
  "preview": { "lines": [ { "sheet": "g&P", "sheetType": "IncomeStatement", "row": 8, "section": null,
                           "label": "Ventas netas", "rowKind": "Detail", "values": { "2023": 172300000.0, "2024": 185450000.0 } } ] },
  "createdAt": "2026-11-18T15:00:00Z"
}
```

`UploadListItemDto`: `{ id, companyId, fileName, fileType, status, years, rowsCount, warningsCount, errorsCount, createdAt, analysisStatus, needsReview, uploadedBy }` (`uploadedBy`: nombre de quien subió el archivo).

Las inconsistencias de calidad nunca bloquean la carga (ver el capítulo *Motores analíticos →
Ingesta y validación*). El archivo original no se guarda.

Desde la etapa 04, el reporte incluye además `analysisStatus` (`NotStarted`, `Completed` o
`Failed`), `needsReview` (revisión recomendada) y `doubtfulAccounts`. El análisis (homologación y
consolidación) se ejecuta automáticamente al subir un archivo válido y la carga queda
`Processed`.

### Homologación — `/api/v1/mappings` (Admin y Analyst)

| Método | Ruta | Request | Response | Errores |
|---|---|---|---|---|
| GET | `/mappings` | — (el Admin elige la empresa con `X-Company-Id`) | 200 `MappingDto[]` | 400 `mapping.company_required` |
| GET | `/mappings/categories` | — | 200 `CategoryDto[]`: `{ category, name, description, statement }` (catálogo en español) | — |
| GET | `/mappings/pending?uploadId=` | — | 200 `PendingAccountDto[]` (dudosas primero, luego por monto) | 404 |
| PUT | `/mappings` | `{ items: [{ id, category }] }` | 200 `MappingDto[]` (confirmados, `method: "Manual"`, confianza 1) | 400 `validation_failed`, 404 |
| POST | `/mappings/consolidate?uploadId=` | — | 200 `ProcessingResult` | 404, 409 `upload.failed` |

- `MappingDto`: `{ id, clientAccount, scope: "Statement" | "Notes", section, role: "Primary" |
  "Breakdown", category, confidence, method, isConfirmed, confirmedAt, confirmedBy }`.
- `PendingAccountDto`: `{ mappingId, clientAccount, originalLabel, sheetName, sheetType,
  rowNumber, section, scope, role, category, confidence, method, isConfirmed, isDoubtful, year,
  amount }`. Una cuenta es dudosa si es `Primary`, no está confirmada y tiene confianza < 0,8.
- `ProcessingResult`: `{ uploadId, status, analysisStatus, needsReview, doubtfulAccounts,
  usedFallback, years }`.
- `consolidate` vuelve a analizar la carga con los mapeos actuales (los confirmados no se tocan;
  los no confirmados se vuelven a sugerir) y actualiza `FinancialData` de todos sus años.

### KPIs y análisis

| Método | Ruta | Rol | Response | Errores |
|---|---|---|---|---|
| GET | `/kpis?year=` | Todos | 200 `KpisResponse` | 400 `kpis.company_required` (Admin sin empresa) |
| GET | `/analysis/summary` | Todos | 200 `AnalysisSummaryDto` | 400 `analysis.company_required` |
| POST | `/analysis/run` | Admin, Analyst | 200 `{ companyId, years, calculatedAt, problems, latestYear, latestScore, latestEstimatedLoss }` | 400 `analysis.company_required`, 403 |

`AnalysisSummaryDto` (lo usa el asistente de carga): `{ companyId, currency, years, latestYear,
kpisCalculated, kpisTotal, problems: { total, high, medium, low }, score, interpretation,
interpretationLabel, estimatedLoss, calculatedAt }`, todo del último año analizado. Sin análisis:
`latestYear: null` y contadores en 0.

`GET /kpis` sin `year` devuelve la serie histórica; con `year`, solo ese año. El análisis
(KPIs → diagnóstico → impacto → score, en una sola transacción) se ejecuta solo al consolidar una
carga; `POST /analysis/run` lo repite bajo demanda.

```json
{
  "companyId": "…", "industry": "Manufacturing", "currency": "DOP",
  "definitions": [ { "code": "EbitdaMargin", "name": "Margen EBITDA", "formula": "EBITDA / Revenue",
                     "unit": "Ratio", "higherIsBetter": true, "dimension": "Profitability", "description": "…" } ],
  "years": [
    { "year": 2024,
      "values": { "EbitdaMargin": 0.1861, "CostEfficiency": 0.8533, "RevenuePerEmployee": 12116066.67, "CostPerUnit": null },
      "normalized": { "EbitdaMargin": 93.05, "RevenuePerEmployee": 78.64, "CostPerUnit": null },
      "trends": { "year": 2024, "previousYear": 2023,
                  "kpis": { "EbitdaMargin": { "previous": 0.1975, "current": 0.1861, "change": -0.0114,
                                              "changePercent": -5.77, "direction": "Down", "favorable": false } },
                  "revenueGrowth": { "previous": 169198600, "current": 181741000, "changePercent": 7.41, "direction": "Up", "favorable": true },
                  "totalCostGrowth": { … }, "overheadGrowth": { … } },
      "calculatedAt": "2026-11-18T15:00:00Z" }
  ],
  "monthly": [ { "year": 2024, "month": 1, "revenue": 13352400, "growthPercent": null,
                 "movingAverage3": null, "seasonalityIndex": 0.864 } ]
}
```

`trends` es `null` en el primer año. `monthly` es `null` si la empresa no tiene serie mensual.

### Problemas — `/api/v1/problems`

| Método | Ruta | Rol | Request | Response | Errores |
|---|---|---|---|---|---|
| GET | `/problems?year=&severity=&category=&status=&page=1&pageSize=20` | Todos | — | 200 `ProblemPage` | 400 `problems.company_required` / `problems.invalid_page` |
| GET | `/problems/{id}` | Todos (otra empresa → 404) | — | 200 `ProblemDetailDto` | 404 |
| PATCH | `/problems/{id}` | Admin, Analyst | `{ "status": "Open" \| "Acknowledged" \| "Resolved" }` | 200 `ProblemDetailDto` | 400, 403, 404 |

- Filtros: `severity` (`High`, `Medium`, `Low`), `category` (`Profitability`, `CostEfficiency`,
  `Productivity`, `FinancialStability`, `Operations`) y `status`. `pageSize` va de 1 a 100.
- Orden de prioridad: severidad y luego impacto económico; `priority` es la posición (1 = más
  urgente).
- `ProblemPage`: `{ items: ProblemListItemDto[], page, pageSize, totalCount, highlights }`.
  `highlights` son los hallazgos positivos del año filtrado o, sin `year`, del último año.
- `ProblemListItemDto`: `{ id, year, code, title, description, category, severity,
  impactCategory, impactValue, status, recommendation, priority }`.
- `ProblemDetailDto`: lo anterior más `aiRecommendation`, `evidence` (JSON con las cifras de la
  regla), `relatedKpi` y `kpiHistory: [{ year, value, normalized }]` (todos los años del KPI
  relacionado).
- El estado que pone el usuario se conserva cuando el análisis se vuelve a ejecutar.
- `impactValue` es la pérdida anual estimada (`null` en las alertas financieras). El detalle del
  cálculo está en `evidence.impact`: `{ estimatedLoss, affectedMarginPp, deteriorationMagnitude,
  severityLevel, formula, reason, inputs }`. La recomendación termina con la frase
  "Impacto estimado: RD$ … en el año, equivalente a … pp de margen."

### Score — `/api/v1/score`

| Método | Ruta | Rol | Response | Errores |
|---|---|---|---|---|
| GET | `/score?year=` | Todos | 200 `ScoreResponse` | 400 `score.company_required` / `score.invalid_year` |

Sin `year` devuelve la serie histórica (un elemento por año); con `year`, solo ese año (lista
vacía si no hay score). Ejemplo (libro sintético de 3 años, 2024):

```json
{
  "companyId": "…",
  "years": [
    { "year": 2024, "totalScore": 83.24, "interpretation": "Healthy",
      "interpretationLabel": "Operación saludable", "changeFromPrevious": 3.04,
      "weightsRedistributed": false,
      "weightsUsed": { "Profitability": 0.3, "CostEfficiency": 0.25, "Productivity": 0.25, "FinancialStability": 0.2 },
      "components": [
        { "dimension": "Profitability", "name": "Rentabilidad", "score": 76.61, "baseWeight": 0.3, "weight": 0.3,
          "inputs": { "EbitdaMargin": 93.05, "GrossMargin": 60.17 }, "missingReason": null },
        { "dimension": "Productivity", "name": "Productividad", "score": 100, "baseWeight": 0.25, "weight": 0.25,
          "inputs": { "RevenuePerEmployee": 100, "RevenuePerEmployeeTrend": null }, "missingReason": null }
      ],
      "calculatedAt": "2026-11-19T15:00:00Z" }
  ]
}
```

Si un componente no tiene datos, `score` es `null`, `weight` es 0, `missingReason` explica por
qué y `weightsRedistributed` es `true`.

### Impacto — `/api/v1/impact`

| Método | Ruta | Rol | Response | Errores |
|---|---|---|---|---|
| GET | `/impact/summary?year=` | Todos | 200 `ImpactSummaryDto` | 400 `impact.company_required` / `impact.invalid_year` |

Sin `year` resume el último año con datos. Una empresa sin datos devuelve `year: null` y montos en 0.

```json
{
  "companyId": "…", "year": 2024, "currency": "DOP",
  "totalEstimatedLoss": 4397941.56, "revenue": 181741000, "lossPercentOfRevenue": 2.42,
  "quantifiedProblems": 4, "unquantifiedProblems": 0,
  "byCategory": [
    { "category": "MarginLoss", "estimatedLoss": 3743864.6, "problems": 2, "share": 85.13 },
    { "category": "ExcessCost", "estimatedLoss": 654076.96, "problems": 2, "share": 14.87 }
  ],
  "top": [
    { "id": "…", "year": 2024, "code": "EBITDA_DECLINE", "title": "Deterioro de rentabilidad",
      "severity": "Low", "impactCategory": "MarginLoss", "impactValue": 2071847.4, "affectedMarginPp": 1.14 }
  ]
}
```

`top` trae hasta 5 problemas ordenados por pérdida (mayor primero). `unquantifiedProblems` cuenta
las alertas financieras y los problemas sin datos para estimar la pérdida.

### Datos — `/api/v1/data`

| Método | Ruta | Rol | Response |
|---|---|---|---|
| GET | `/data?type=&year=&sheetType=&uploadId=` | Todos (el Admin elige la empresa con `X-Company-Id` o `companyId`) | 200 `DataResponse` · 400 `data.company_required` |

`type`: `Raw` (por defecto: filas en bruto de la carga más reciente que tenga el año, filtrables
por `sheetType` o `uploadId`), `Financial` (datos consolidados; se llenan con la homologación de
la etapa 04: se llenan al subir), `Operational` o `Monthly`. La respuesta trae `{ companyId, type, year, uploadId,
availableYears, raw | financial | operational | monthly }`; solo se llena la lista del tipo pedido.
Cada fila `raw`: `{ sheet, sheetType, row, section, originalLabel, normalizedLabel, accountCode,
rowKind, values: { "2023": …, "2024": … } }`.

### Sistema

| Método | Ruta | Rol | Response |
|---|---|---|---|
| GET | `/health` | Público | 200/503 `{ status, service, version, checks: [self, database] }` |
| GET | `/api/v1/system/info` | Público | 200 `{ name, version, environment }` |
