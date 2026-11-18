# Etapa 02 — Autenticación y roles

**Depende de:** 01 · **Rama:** `etapa-02-autenticacion`

## Prompt

> Implementa la autenticación de Opsynex AI en ASP.NET Core (secciones 8.1 y 19 de las
> especificaciones): registro, login con JWT, contraseñas con hash seguro y control de acceso
> por roles Admin / Analyst / Client con aislamiento por empresa. Incluye tests completos.

## Tareas

1. `AuthController` en `/api/v1/auth`:
   - `POST /register`: **solo un Admin** crea usuarios, de cualquier rol (`Admin`, `Analyst`
     o `Client`), y los asigna a una empresa. **No existe auto-registro público** (requisito del
     cliente): sin token de Admin el endpoint responde 401 o 403. Validación con
     FluentValidation: email válido y contraseña de mínimo 8 caracteres con letra y número.
   - `POST /login`: devuelve `accessToken` (JWT con claims `sub`, `email`, `role` y
     `company_id`; expiración configurable) y los datos del usuario.
   - `GET /me`: usuario actual.
2. Hash con `PasswordHasher<User>`. Bloqueo temporal tras 5 intentos fallidos.
3. `CompaniesController` y `UsersController` (CRUD solo para Admin).
4. **Autorización**: políticas `CanManage` (Admin), `CanAnalyze` (Admin, Analyst) y `CanView`
   (todos). Servicio `ICurrentUser` que alimenta el filtro global de empresa de EF Core. Un
   Admin puede indicar `companyId` en query o header; el resto queda fijo a su empresa.
5. Matriz de permisos:
   | Acción | Admin | Analyst | Client |
   |---|---|---|---|
   | Gestionar empresas y usuarios | ✅ | ❌ | ❌ |
   | Subir archivos, homologar y recalcular | ✅ | ✅ | ❌ |
   | Ver KPIs, problemas, score y dashboard | ✅ | ✅ | ✅ |
6. Seed del primer Admin desde configuración (`Seed:AdminEmail` y `Seed:AdminPassword`) o
   con el comando `dotnet run --project src/Opsynex.Api -- seed-admin`.
7. Errores uniformes con ProblemDetails (`code`: `auth.invalid_credentials`, `auth.forbidden`, etc.).

## Criterios de aceptación

- [ ] Las contraseñas nunca se guardan ni se devuelven en texto plano.
- [ ] Token inválido o vencido → 401; rol insuficiente → 403; recurso de otra empresa → 404.
- [ ] Los tests de integración de cada endpoint y de la matriz de permisos pasan.

## Documentación a actualizar

- `04_api.md`: endpoints de auth, empresas y usuarios (método, ruta, rol, request, response y errores).
- `02_arquitectura.md`: sección de seguridad y flujo de autenticación (diagrama de secuencia Mermaid).
- `13_bitacora.md`.
