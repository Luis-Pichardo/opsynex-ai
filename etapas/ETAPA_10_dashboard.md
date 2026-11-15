# Etapa 10 — Dashboard ejecutivo y vista de problemas (cierre del MVP)

**Depende de:** 07 y 09 · **Rama:** `etapa-10-dashboard`

## Prompt

> Construye el dashboard ejecutivo y la vista de problemas de Opsynex AI (secciones 8.6 y 14 de
> las especificaciones) con Recharts. Al terminar, verifica punto por punto la "Definición MVP"
> (sección 20) y deja el MVP listo para demo.

## Tareas

1. **Dashboard principal** (`/dashboard`):
   - **Score general**: medidor circular 0–100 con color por rango e interpretación, variación
     contra el año anterior y desglose de los 4 componentes (barras con peso).
   - **Resumen de KPIs**: tarjetas (EBITDA margin, margen bruto, cost efficiency, revenue per
     employee, operating cost ratio) con valor, variación YoY y flecha de tendencia
     (verde/rojo según si es favorable).
   - **Tendencias**: gráfico de líneas multi-año de KPIs (selector de KPI) y barras de revenue vs. costos.
   - **Principales problemas**: top 5 por prioridad con severidad e impacto.
   - **Impacto financiero estimado**: total y por categoría (dona o barras).
   - **Recomendaciones**: lista de las recomendaciones de los problemas principales.
2. **Lista de problemas** (`/problems`): tabla con filtros (año, severidad, categoría, estado),
   orden por impacto, badges de severidad.
3. **Detalle de problema** (`/problems/:id`): descripción, nivel de severidad, impacto económico
   estimado (con explicación del cálculo), comportamiento histórico (gráfico del KPI
   relacionado), evidencia y recomendaciones sugeridas; cambiar estado (Admin/Analyst).
4. **Página KPIs** (`/kpis`): tabla histórica de todos los KPIs con tendencias y gráfico por KPI.
5. Formato de moneda y porcentajes según la moneda de la empresa; estados de carga y vacíos.
6. Exportar el dashboard a PDF (impresión del navegador con estilos `@media print`) — opcional
   si queda tiempo.
7. **Checklist MVP**: agrega una sección "Checklist MVP" en `docs/documentacion/08_pruebas.md`
   verificando cada punto de la sección 20 con evidencia (test o captura).

## Criterios de aceptación

- [ ] Con `sample_data/` el dashboard muestra score, KPIs, tendencias, problemas, impacto y recomendaciones.
- [ ] El detalle de problema muestra todos los elementos de la sección 14.
- [ ] Usable en móvil (375 px) y escritorio.
- [ ] Toda la "Definición MVP" de ambas especificaciones está cumplida y documentada.

## Documentación a actualizar

- `09_guia_usuario.md`: cómo leer el dashboard, el score y los problemas (con capturas si es posible:
  guárdalas en `docs/documentacion/img/` y referéncialas desde el Markdown).
- `06_frontend.md`, `08_pruebas.md` (checklist MVP), `01_proyecto.md` (estado: MVP completo).
- `12_bitacora.md`.
