# Etapa 05 — Motor de cálculo de KPIs

**Depende de:** 04 · **Rama:** `etapa-05-motor-kpis`

## Prompt

> Implementa el motor de KPIs en C# (secciones 8.3 y 9 de las especificaciones): cálculo
> automático, almacenamiento histórico, normalización y evaluación de tendencias. Se ejecuta
> automáticamente al consolidar datos y también bajo demanda.

## Tareas

1. `Opsynex.Domain/Engines/KpiEngine.cs` (clase pura; entrada: datos financieros y
   operacionales de un año):
   | KPI | Fórmula |
   |---|---|
   | `EbitdaMargin` | EBITDA / Revenue |
   | `GrossMargin` | (Revenue − COGS) / Revenue |
   | `CostEfficiency` | Total Cost / Revenue |
   | `RevenuePerEmployee` | Revenue / Number of Employees |
   | `OperatingCostRatio` | Operating Expenses / Revenue |
   | `OverheadRatio` | Overhead Cost / Revenue (auxiliar para el diagnóstico) |
   | `EnergyCostRatio` | Energy Cost / Revenue (auxiliar) |
   | `CostPerUnit` | Total Cost / Units Produced (si hay datos operacionales) |
   Devuelve `null` si falta un dato o el denominador es 0.
2. **Tendencias** (`TrendAnalyzer`): para cada KPI, variación absoluta y porcentual año contra
   año, dirección (`Up` / `Down` / `Stable`, con umbral configurable de ±1 %) y si es favorable
   (bajar `CostEfficiency` es favorable). También el crecimiento año a año de Revenue, TotalCost
   y Overhead, que usa el diagnóstico.
3. **Normalización** a escala 0–100 con rangos de referencia por industria (`Manufacturing`,
   `Services`) en `appsettings` (sección `Benchmarks`) con un objeto de opciones tipado. Por
   ejemplo, margen EBITDA en manufactura: 0 % → 0 pts y 20 % → 100 pts. La usa el score (etapa 07).
4. Agrega a `KpiResult` lo que haga falta (`OverheadRatio`, `CostPerUnit`, `Trends` JSON,
   `Normalized` JSON) con su migración.
5. `AnalysisPipeline` en Application: `RunAsync(companyId)` recalcula los KPIs de todos los años
   (en las etapas siguientes también diagnóstico, impacto y score). Se llama al consolidar y
   desde `POST /api/v1/analysis/run`.
6. `GET /api/v1/kpis?year=`: KPIs del año con sus tendencias; sin `year` devuelve la serie histórica.

## Criterios de aceptación

- [ ] Tests unitarios (xUnit `[Theory]`) de cada fórmula, incluidos casos borde (cero, null y negativos).
- [ ] Con `sample-data/`, los KPIs coinciden con valores calculados a mano en el test.
- [ ] `GET /kpis` devuelve la serie 2022–2024 con tendencias.

## Documentación a actualizar

- `05_motores_analiticos.md`: "Motor de KPIs" (fórmulas, interpretación, tendencias, normalización y benchmarks).
- `04_api.md`: `/kpis` y `/analysis/run`.
- `13_bitacora.md`.
