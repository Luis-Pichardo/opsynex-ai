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
| 413 | `upload.too_large` | El archivo supera `Upload:MaxMb` (20 MB) |
| 422 | `upload.*` (en `errors[]` del reporte) | Error bloqueante del archivo: `empty_file`, `invalid_signature`, `unreadable`, `no_financial_statements`, `years_not_found`, `file_type_required` |
| 500 | `internal_error` | Error inesperado (se registra en el log con su `traceId`) |

### Roles y permisos

Políticas de autorización: `CanManage` (Admin), `CanAnalyze` (Admin y Analyst) y `CanView`
(todos los roles).

| Acción | Admin | Analyst | Client |
|---|---|---|---|
| Gestionar empresas y usuarios | ✅ | ❌ | ❌ |
| Subir archivos y descargar plantillas (etapa 03); homologar y recalcular (etapas 04–07) | ✅ | ✅ | ❌ |
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

`UploadListItemDto`: `{ id, companyId, fileName, fileType, status, years, rowsCount, warningsCount, errorsCount, createdAt }`.

Las inconsistencias de calidad nunca bloquean la carga (ver el capítulo *Motores analíticos →
Ingesta y validación*). El archivo original no se guarda.

### Datos — `/api/v1/data`

| Método | Ruta | Rol | Response |
|---|---|---|---|
| GET | `/data?type=&year=&sheetType=&uploadId=` | Todos (el Admin elige la empresa con `X-Company-Id` o `companyId`) | 200 `DataResponse` · 400 `data.company_required` |

`type`: `Raw` (por defecto: filas en bruto de la carga más reciente que tenga el año, filtrables
por `sheetType` o `uploadId`), `Financial` (datos consolidados; se llenan con la homologación de
la etapa 04), `Operational` o `Monthly`. La respuesta trae `{ companyId, type, year, uploadId,
availableYears, raw | financial | operational | monthly }`; solo se llena la lista del tipo pedido.
Cada fila `raw`: `{ sheet, sheetType, row, section, originalLabel, normalizedLabel, accountCode,
rowKind, values: { "2023": …, "2024": … } }`.

### Sistema

| Método | Ruta | Rol | Response |
|---|---|---|---|
| GET | `/health` | Público | 200/503 `{ status, service, version, checks: [self, database] }` |
| GET | `/api/v1/system/info` | Público | 200 `{ name, version, environment }` |

### Próximos endpoints

| Grupo | Endpoints | Etapa |
|---|---|---|
| Homologación | mapeo de cuentas | 04 |
| KPIs | `GET /kpis` | 05 |
| Problemas | `GET /problems`, `GET /problems/{id}`, `PATCH /problems/{id}` | 06 |
| Score | `GET /score` | 07 |

> Pendiente (Etapas 04 a 07): especificación detallada de esos endpoints.
