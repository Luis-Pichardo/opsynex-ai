# Etapa 13 — Seguridad, calidad y datos de demostración

**Depende de:** 10 (idealmente 11 y 12) · **Rama:** `etapa-13-calidad-y-demo`

## Prompt

> Endurece la seguridad (sección 19), sube la cobertura de pruebas, agrega pruebas end-to-end
> y prepara un entorno de demostración con datos realistas para presentar al cliente.

## Tareas

1. **Seguridad**
   - Revisión de la validación de entradas (FluentValidation en todos los endpoints) y de
     archivos (tamaño, magic bytes, límite de filas, sin fórmulas ni macros).
   - Rate limiting (`Microsoft.AspNetCore.RateLimiting`) en `/auth/login` y en `/uploads`.
   - Cabeceras de seguridad (HSTS, X-Content-Type-Options, CSP en el frontend) y CORS restrictivo.
   - Secretos solo por variables de entorno o Key Vault/Secrets Manager; logs sin datos sensibles.
   - Test automático que recorre **todos** los endpoints y verifica el aislamiento por empresa
     y los roles.
   - `dotnet list package --vulnerable`, `pip-audit` y `npm audit`: corregir todo lo crítico.
2. **Calidad**: cobertura ≥ 80 % en Domain y Application (coverlet), ≥ 80 % en `ai-service`;
   tests de las páginas principales del frontend; logs estructurados con correlation id
   (también hacia el servicio de IA).
3. **E2E** con Playwright (`e2e/`): login → carga de `sample-data` → homologación → dashboard →
   detalle de problema; y permisos del rol `Client`. Se ejecutan en CI.
4. **Datos demo**: comando `dotnet run --project src/Opsynex.Api -- seed-demo` que crea 2
   empresas (una manufacturera con datos industriales y una de servicios) y usuarios de cada
   rol con contraseñas de demo documentadas, y luego ejecuta el análisis.
5. **Rendimiento**: índices revisados, paginación en todos los listados, consultas
   `AsNoTracking` para lecturas, y compresión de respuestas.
6. **Revisión de UI**: auditoría de toda la app contra el checklist de accesibilidad de la
   guía de diseño (sección 10) y corrección de lo que falte.

## Criterios de aceptación

- [ ] Las coberturas objetivo se cumplen y los E2E pasan en CI.
- [ ] No hay vulnerabilidades críticas en dependencias.
- [ ] `seed-demo` deja la plataforma lista para presentar en menos de 1 minuto.
- [ ] El checklist de accesibilidad de la guía se cumple en todas las pantallas.

## Documentación a actualizar

- `09_pruebas.md`: estrategia, cobertura, E2E y cómo ejecutarlos.
- `02_arquitectura.md`: sección de seguridad completa.
- `10_guia_usuario.md`: usuarios de demostración.
- `13_bitacora.md`.
