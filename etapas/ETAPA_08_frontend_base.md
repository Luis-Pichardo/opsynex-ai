# Etapa 08 — Frontend base y sistema de diseño

**Depende de:** 02 (puede hacerse en paralelo con 03–07) · **Rama:** `etapa-08-frontend-base`

## Prompt

> Construye la base del frontend de Opsynex AI siguiendo **al pie de la letra**
> `docs/diseno/GUIA_DE_DISENO.md`: tokens de diseño, componentes shadcn/ui personalizados,
> cliente de API, autenticación con JWT, rutas protegidas por rol y el layout principal. Debe
> verse como un SaaS financiero profesional, en modo claro y oscuro, y ser responsive.

## Tareas

1. **Tokens y tema**: `src/styles/tokens.css` (copia exacta de `docs/diseno/tokens.css`),
   `tailwind.config` con todos los tokens mapeados, la fuente Inter y un `ThemeProvider`
   (claro, oscuro o sistema) que pone `data-theme` en `<html>` y recuerda la preferencia.
2. **Componentes** (shadcn/ui, adaptados a la guía) en `src/components/ui/`: Button, Input,
   Select, Label, Card, Badge (variantes `critical`, `serious`, `warning`, `good` e `info`,
   con icono incluido), Table (sobre TanStack Table), Tabs, Dialog, Sheet, DropdownMenu,
   Tooltip, Skeleton, Sonner (toasts), Form (React Hook Form + Zod). Componentes propios en
   `src/components/common/`: `StatCard`, `EmptyState`, `PageHeader`, `ScoreGauge`,
   `SeverityBadge` y `TrendIndicator` (verde o rojo según si la variación es favorable).
3. `src/lib/format.ts`: `formatCurrency` (compacto y completo), `formatPercent`, `formatPp`,
   `formatNumber` y `formatDate` con `Intl` en español, con tests.
4. **Página interna `/design`** (solo en desarrollo): muestra todos los componentes y la
   paleta, para revisar visualmente que cumplen la guía.
5. **Cliente de API** `src/api/client.ts` con `VITE_API_URL`, token en la cabecera, manejo de
   401 (cierra sesión) y errores ProblemDetails → toast. Tipos en `src/types/`.
6. **Autenticación**: páginas Login (diseño a pantalla dividida: panel de marca con el color
   del sidebar y formulario), sin página de registro (los usuarios los crea el Admin), "¿Olvidaste tu contraseña?" indica contactar al administrador, `AuthContext`, persistencia
   del token, `ProtectedRoute` y `RoleGuard`.
7. **Layout**: sidebar de 256 px (colapsable a 72 px), oscura, con iconos lucide (Dashboard,
   Problemas, KPIs, Carga de datos, Homologación, Producción*, Administración). Solo muestra lo
   que el rol permite. Topbar con título, selector de empresa (Admin), selector de año, botón
   de tema y menú de usuario. En móvil, la sidebar es un drawer (Sheet). (*Producción se
   habilita en la etapa 12.)
8. **Administración** (Admin): tablas y formularios de empresas y usuarios.
9. Páginas con `EmptyState` para las secciones que se completan en las etapas 09 y 10.

## Criterios de aceptación

- [ ] No hay ningún color hex, tamaño de fuente arbitrario ni sombra fuera de los tokens
      (verificado con una búsqueda en `src/`, excepto `tokens.css`).
- [ ] Login contra el backend real; el logout y el token vencido redirigen al login.
- [ ] Un `Client` no ve Carga de datos, Homologación ni Administración, ni por URL directa.
- [ ] Se ve bien en 375 px y en escritorio, en modo claro y oscuro (con capturas en el PR).
- [ ] `npm test`, `npm run lint` y `npm run build` pasan; hay tests de format, AuthContext y RoleGuard.

## Documentación a actualizar

- `07_frontend.md`: estructura, sistema de diseño implementado, rutas y permisos, manejo de estado.
- `10_guia_usuario.md`: inicio de sesión, navegación y administración de usuarios.
- `13_bitacora.md`.
