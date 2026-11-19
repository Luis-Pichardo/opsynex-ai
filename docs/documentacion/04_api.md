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
| 500 | `internal_error` | Error inesperado (se registra en el log con su `traceId`) |

### Roles y permisos

Políticas de autorización: `CanManage` (Admin), `CanAnalyze` (Admin y Analyst) y `CanView`
(todos los roles).

| Acción | Admin | Analyst | Client |
|---|---|---|---|
| Gestionar empresas y usuarios | ✅ | ❌ | ❌ |
| Subir archivos, homologar y recalcular (etapas 03–07) | ✅ | ✅ | ❌ |
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

### Sistema

| Método | Ruta | Rol | Response |
|---|---|---|---|
| GET | `/health` | Público | 200/503 `{ status, service, version, checks: [self, database] }` |
| GET | `/api/v1/system/info` | Público | 200 `{ name, version, environment }` |

### Próximos endpoints

| Grupo | Endpoints | Etapa |
|---|---|---|
| Datos | `POST /uploads`, `GET /data` | 03 |
| Homologación | mapeo de cuentas | 04 |
| KPIs | `GET /kpis` | 05 |
| Problemas | `GET /problems`, `GET /problems/{id}`, `PATCH /problems/{id}` | 06 |
| Score | `GET /score` | 07 |

> Pendiente (Etapas 03 a 07): especificación detallada de esos endpoints.
