# Etapa 08 — Frontend base (autenticación, layout, navegación)

**Depende de:** 02 (puede hacerse en paralelo a 03–07) · **Rama:** `etapa-08-frontend-base`

## Prompt

> Construye la base del frontend de Opsynex AI en React + TypeScript + Tailwind: sistema de
> diseño, cliente de API, autenticación con JWT, rutas protegidas por rol y el layout principal
> de la aplicación. Debe verse profesional (SaaS financiero-ejecutivo) y ser responsive.

## Tareas

1. **Sistema de diseño** en Tailwind: paleta (azul oscuro/índigo corporativo + colores
   semánticos para severidad high/medium/low y score), tipografía (Inter), modo claro y oscuro.
   Componentes en `src/components/ui/`: Button, Input, Select, Card, Badge, Table, Modal, Toast,
   Spinner, EmptyState, StatCard.
2. **Cliente API** `src/api/client.ts` (fetch o axios) con `VITE_API_URL`, token en el header,
   manejo de 401 (cerrar sesión) y errores uniformes. Tipos TypeScript de las respuestas en `src/types/`.
3. **Auth**: páginas Login y Registro (si está habilitado), `AuthContext` con usuario y rol,
   persistencia del token, `ProtectedRoute` y `RoleGuard`.
4. **Layout**: sidebar colapsable (Dashboard, Problemas, KPIs, Carga de datos, Homologación,
   Producción*, Administración) que muestra solo lo permitido por rol; topbar con selector de
   empresa (Admin), selector de año y menú de usuario. En móvil, menú tipo drawer.
   (*Producción se habilita en la Etapa 11.)
5. **Administración** (solo Admin): listar/crear empresas y usuarios.
6. Páginas vacías con `EmptyState` para las secciones que se completan en 09 y 10.
7. TanStack Query para datos del servidor; estado global mínimo (empresa y año seleccionados).

## Criterios de aceptación

- [ ] Login funciona contra el backend real; logout y expiración de token redirigen al login.
- [ ] Un `client` no ve Carga de datos, Homologación ni Administración (ni por URL directa).
- [ ] Se ve bien en 375 px (móvil) y en escritorio.
- [ ] `npm test`, `npm run lint` y `npm run build` pasan; hay tests de AuthContext y RoleGuard.

## Documentación a actualizar

- `06_frontend.md`: estructura, sistema de diseño, rutas y permisos por rol, manejo de estado.
- `09_guia_usuario.md`: inicio de sesión, navegación, administración de usuarios.
- `12_bitacora.md`.
