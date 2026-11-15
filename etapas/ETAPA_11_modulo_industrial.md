# Etapa 11 — Módulo industrial (producción, OEE, scrap, downtime)

**Depende de:** 10 · **Rama:** `etapa-11-modulo-industrial`

## Prompt

> Implementa el módulo industrial descrito en `docs/especificaciones/PLATAFORMA_DE_ANALISIS`
> (y la sección 10 de ESPECIFICACIONES): datos de producción y costos por fecha, máquina y
> línea; KPIs industriales (OEE, scrap rate, downtime ratio, cost per unit, capacity
> utilization); reglas de diagnóstico e impacto industrial; componente industrial en el score;
> y pantallas de Producción y Costos. Reutiliza el pipeline, problemas y score existentes.

## Tareas

1. **Modelo** (migración): `production_data` (company_id, date, machine_id, line_id,
   units_produced, downtime_minutes, planned_minutes, cycle_time, good_units, total_units),
   `cost_data` (company_id, date, machine_id, labor_cost, material_cost, energy_cost,
   overhead_cost), `standard_data` (company_id, process_name / machine_id,
   standard_cycle_time, standard_cost, cost_per_minute), `industrial_kpi_results`
   (company_id, period, machine_id/line_id nullable, oee, availability, performance, quality,
   scrap_rate, downtime_ratio, cost_per_unit, capacity_utilization).
2. **Carga**: nuevos `file_type` `production`, `costs`, `standards` con plantillas,
   validación y datos de ejemplo (`sample_data/industrial/`, 3 máquinas, 2 líneas, 3 meses).
3. **KPIs industriales**:
   - Availability = (planned − downtime) / planned
   - Performance = (standard_cycle_time × total_units) / run_time
   - Quality = good_units / total_units
   - OEE = Availability × Performance × Quality
   - Scrap rate = (total_units − good_units) / total_units
   - Cost per unit = total_cost / units_produced
   - Downtime ratio, capacity utilization. Agregados por máquina, línea, mes y total.
4. **Reglas** (configurables): `LOW_OEE` (OEE < 0.60 → high), `HIGH_SCRAP` (scrap > 0.05 →
   medium), `HIGH_DOWNTIME` (downtime > umbral → high), `COST_PER_UNIT_ABOVE_STANDARD`.
5. **Impacto**: `downtime_cost = downtime_minutes × cost_per_minute`,
   `scrap_cost = (total_units − good_units) × unit_cost`, `total_impact = downtime_cost + scrap_cost`.
6. **Score industrial**: `production_score × 0.30 + cost_score × 0.25 + financial_score × 0.25
   + standard_score × 0.20`. Muéstralo junto al Score Opsynex cuando haya datos industriales.
7. Endpoints `/api/v1/industrial/kpis`, `/industrial/score` (filtros por periodo, máquina, línea).
8. **Frontend**: páginas Producción (OEE con sus 3 componentes, downtime por máquina, ranking
   de máquinas) y Costos (costo por unidad vs. estándar, composición de costos). Habilitar el
   ítem "Producción" del menú cuando la empresa tenga datos industriales.

## Criterios de aceptación

- [ ] Con `sample_data/industrial/` se detectan los 3 problemas de la especificación (Low OEE,
      High Scrap, High Downtime) con su impacto.
- [ ] Tests de todas las fórmulas industriales.
- [ ] Pantallas de Producción y Costos funcionan en móvil y escritorio.

## Documentación a actualizar

- `03_modelo_datos.md`, `05_motores_analiticos.md` (sección industrial), `04_api.md`,
  `06_frontend.md`, `09_guia_usuario.md`, `12_bitacora.md`.
