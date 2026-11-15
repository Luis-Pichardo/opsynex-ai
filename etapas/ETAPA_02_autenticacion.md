# Etapa 02 — Autenticación y roles

**Depende de:** 01 · **Rama:** `etapa-02-autenticacion`

## Prompt

> Implementa el módulo de autenticación de Opsynex AI (sección 8.1 y 19 de las
> especificaciones): registro, login con JWT, contraseñas con bcrypt y control de acceso por
> roles Admin / Analyst / Client, con aislamiento por empresa. Incluye tests completos.

## Tareas

1. Endpoints en `/api/v1/auth`:
   - `POST /register` — crea usuario. Solo un Admin puede crear usuarios con rol `admin` o
     `analyst`; el auto-registro público crea `client` (o deshabilítalo con una variable
     `ALLOW_PUBLIC_REGISTER`). Valida email y contraseña (mín. 8, letra y número).
   - `POST /login` — devuelve `access_token` (JWT, expiración configurable) y datos del usuario.
   - `GET /me` — usuario actual.
2. Endpoints de administración `/api/v1/companies` y `/api/v1/users` (CRUD solo Admin).
3. Dependencias FastAPI: `get_current_user`, `require_roles(...)`, `get_company_scope` (Admin
   puede pasar `company_id`; los demás quedan fijados a su empresa).
4. Matriz de permisos:
   | Acción | Admin | Analyst | Client |
   |---|---|---|---|
   | Gestionar empresas y usuarios | ✅ | ❌ | ❌ |
   | Subir archivos / homologar / recalcular | ✅ | ✅ | ❌ |
   | Ver KPIs, problemas, score, dashboard | ✅ | ✅ | ✅ |
5. Comando CLI `python -m app.cli create-admin --email ... --password ...` para el primer admin.
6. Errores con formato uniforme `{ "detail": "...", "code": "..." }`.

## Criterios de aceptación

- [ ] Contraseñas nunca se guardan ni devuelven en texto plano.
- [ ] Token inválido o vencido → 401; rol insuficiente → 403; datos de otra empresa → 404.
- [ ] Tests de cada endpoint y de la matriz de permisos pasan.

## Documentación a actualizar

- `04_api.md`: endpoints de auth, empresas y usuarios (método, ruta, rol, request, response, errores).
- `02_arquitectura.md`: sección de seguridad y flujo de autenticación.
- `12_bitacora.md`.
