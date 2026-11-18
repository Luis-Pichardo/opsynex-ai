# Etapa 10 — Dashboard ejecutivo y vista de problemas (cierre del MVP)

**Depende de:** 07 y 09 · **Rama:** `etapa-10-dashboard`

## Prompt

> Construye el dashboard ejecutivo y la vista de problemas de Opsynex AI (secciones 8.6 y 14
> de las especificaciones) con Recharts, siguiendo las secciones 2.4, 5 y 7 de
> `docs/diseno/GUIA_DE_DISENO.md`. Al terminar, verifica punto por punto la "Definición MVP"
> (sección 20) y deja el MVP listo para una demo.

## Tareas

1. **Dashboard** (`/dashboard`), en el orden de la guía:
   - **Score**: `ScoreGauge` con color e interpretación del rango, variación contra el año
     anterior y desglose de los 4 componentes (barras horizontales con su peso).
   - **Impacto financiero estimado**: total destacado y desglose por categoría (barras horizontales).
   - **KPIs**: `StatCard` de EBITDA margin, margen bruto, cost efficiency, revenue per employee
     y operating cost ratio, con variación YoY y `TrendIndicator`.
   - **Tendencias**: líneas multi-año con selector de KPI, y barras de revenue vs. costos
     (colores fijos por entidad según la guía).
   - **Principales problemas**: top 5 por prioridad con `SeverityBadge` e impacto.
   - **Recomendaciones**: las de los problemas principales.
2. **Lista de problemas** (`/problems`): tabla con filtros (año, severidad, categoría, estado),
   orden por impacto y paginación.
3. **Detalle de problema** (`/problems/:id`): descripción, severidad, impacto económico con la
   explicación del cálculo, comportamiento histórico (gráfico del KPI relacionado), evidencia
   y recomendaciones; cambio de estado (Admin y Analyst).
4. **KPIs** (`/kpis`): tabla histórica de todos los KPIs con tendencias y un gráfico por KPI.
5. Todos los gráficos con tooltip, leyenda, `aria-label` y enlace "Ver tabla". Skeletons
   mientras cargan y `EmptyState` si no hay datos ("Sube tus estados financieros para empezar").
6. **Informe PDF (obligatorio)**: botón "Descargar informe PDF" en el dashboard y en el
   detalle de problema (todos los roles, incluido `Client`).
   - Generación en el **backend** con QuestPDF (`Infrastructure/Reports`), endpoint
     `GET /api/v1/reports/executive?year=` → `application/pdf`. Revisa la licencia de QuestPDF
     (la Community es gratuita para organizaciones con ingresos anuales menores a 1 M USD) y
     anótala en la documentación; si no aplica, usa una alternativa libre y explícalo.
   - Contenido: portada (empresa, año, fecha de generación), score con interpretación y
     componentes, impacto total y por categoría, tabla de KPIs con variación anual, gráfico de
     tendencia (SVG generado en C#), lista de problemas con severidad, impacto y
     recomendación, advertencias de calidad de los datos y una nota metodológica breve. Desde
     la etapa 11 incluye el resumen ejecutivo de la IA.
   - Respeta la guía de diseño (colores, tipografía Inter o la que esté embebida, formatos
     `RD$`), en tamaño Carta, con número de página y nombre de la empresa en el pie.
   - Tests: el PDF se genera, tiene más de 1 página con datos sintéticos y contiene el score y
     los títulos de los problemas (extrae el texto en el test).
7. **Checklist MVP**: agrega una sección "Checklist MVP" en `docs/documentacion/09_pruebas.md`
   verificando cada punto de la sección 20 con evidencia (test o captura).

## Criterios de aceptación

- [ ] Con `sample-data/`, el dashboard muestra score, impacto, KPIs, tendencias, problemas y recomendaciones.
- [ ] El detalle de problema muestra todos los elementos de la sección 14.
- [ ] El botón "Descargar informe PDF" funciona para Admin, Analyst y Client, y el PDF tiene
      todo el contenido del punto 6 (con capturas del PDF en el PR).
- [ ] Cumple la guía de diseño en 375 px y en escritorio, en ambos temas (capturas en el PR).
- [ ] Toda la "Definición MVP" de ambas especificaciones está cumplida y documentada.

## Documentación a actualizar

- `10_guia_usuario.md`: cómo leer el dashboard, el score y los problemas (con capturas).
- `07_frontend.md`, `09_pruebas.md` (Checklist MVP) y `01_proyecto.md` (estado: MVP completo).
- `13_bitacora.md`.
