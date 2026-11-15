# Etapa 12 — Seguridad, calidad y datos de demostración

**Depende de:** 10 (idealmente 11) · **Rama:** `etapa-12-calidad-y-demo`

## Prompt

> Endurece la seguridad (sección 19), sube la cobertura de pruebas, agrega pruebas end-to-end y
> prepara un entorno de demostración con datos realistas para presentar al cliente.

## Tareas

1. **Seguridad**: revisión de validación de inputs (Pydantic estricto), validación de archivos
   (tamaño, tipo real, fórmulas/macros en Excel ignoradas, límite de filas), rate limiting en
   `/login`, cabeceras de seguridad, CORS restrictivo, secretos solo por entorno, logs sin datos
   sensibles, revisión de aislamiento por empresa en **todos** los endpoints (test que lo
   recorra automáticamente). Ejecuta `pip-audit` y `npm audit` y corrige lo crítico.
2. **Calidad**: cobertura backend ≥ 80 % (`pytest --cov`), tests de frontend de las páginas
   principales, manejo de errores consistente, logs estructurados.
3. **E2E** con Playwright (`e2e/`): login → carga de `sample_data` → homologación → dashboard →
   detalle de problema; y verificación de permisos del rol `client`.
4. **Datos demo**: comando `python -m app.cli seed-demo` que crea 2 empresas (una
   manufacturera con datos industriales y una de servicios), usuarios de cada rol con
   contraseñas de demo documentadas, y ejecuta el análisis.
5. **Rendimiento**: índices en columnas de filtro (company_id, year, date), paginación en listados.

## Criterios de aceptación

- [ ] Cobertura backend ≥ 80 %; E2E pasan en CI.
- [ ] Sin vulnerabilidades críticas en `pip-audit` / `npm audit`.
- [ ] `seed-demo` deja la plataforma lista para presentar en < 1 minuto.

## Documentación a actualizar

- `08_pruebas.md`: estrategia, cobertura, E2E y cómo ejecutarlos.
- `02_arquitectura.md`: sección de seguridad completa.
- `09_guia_usuario.md`: usuarios de demostración.
- `12_bitacora.md`.
