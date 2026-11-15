# Etapa 12 — Módulo industrial (producción, OEE, scrap, downtime)

**Depende de:** 10 · **Rama:** `etapa-12-modulo-industrial`

## Prompt

> Implementa el módulo industrial descrito en `docs/especificaciones/PLATAFORMA_DE_ANALISIS`
> (y en la sección 10 de ESPECIFICACIONES): datos de producción y costos por fecha, máquina y
> línea; KPIs industriales; reglas de diagnóstico e impacto industrial; el score industrial y
> las pantallas de Producción y Costos. Reutiliza el pipeline, los problemas y el score existentes.

## Tareas

1. **Modelo** (EF Core y migración): `ProductionData` (CompanyId, Date, MachineId, LineId,
   UnitsProduced, DowntimeMinutes, PlannedMinutes, CycleTime, GoodUnits, TotalUnits), `CostData`
   (CompanyId, Date, MachineId, LaborCost, MaterialCost, EnergyCost, OverheadCost),
   `StandardData` (CompanyId, ProcessName/MachineId, StandardCycleTime, StandardCost,
   CostPerMinute) e `IndustrialKpiResult` (CompanyId, Period, MachineId/LineId nullable, Oee,
   Availability, Performance, Quality, ScrapRate, DowntimeRatio, CostPerUnit, CapacityUtilization).
2. **Carga**: nuevos `FileType` `Production`, `Costs` y `Standards`, con plantillas, validación
   y datos de ejemplo (`sample-data/industrial/`: 3 máquinas, 2 líneas, 3 meses).
3. **KPIs industriales** (`Opsynex.Domain/Engines/Industrial/`):
   - Availability = (planificado − downtime) / planificado
   - Performance = (standard_cycle_time × total_units) / run_time
   - Quality = good_units / total_units
   - OEE = Availability × Performance × Quality
   - Scrap rate = (total_units − good_units) / total_units
   - Cost per unit = total_cost / units_produced
   - Downtime ratio y capacity utilization. Agregados por máquina, línea, mes y total.
4. **Reglas** configurables: `LOW_OEE` (OEE < 0,60 → High), `HIGH_SCRAP` (scrap > 0,05 →
   Medium), `HIGH_DOWNTIME` (downtime sobre el umbral → High) y `COST_PER_UNIT_ABOVE_STANDARD`.
5. **Impacto**: `downtime_cost = downtime_minutes × cost_per_minute`,
   `scrap_cost = (total_units − good_units) × unit_cost` y `total_impact = downtime_cost + scrap_cost`.
6. **Score industrial**: `production_score × 0,30 + cost_score × 0,25 + financial_score × 0,25 +
   standard_score × 0,20`. Se muestra junto al Score Opsynex cuando hay datos industriales.
7. La detección de anomalías del servicio de IA también se aplica a las series diarias de
   OEE y downtime (aquí sí hay datos suficientes para estadística).
8. Endpoints `/api/v1/industrial/kpis` y `/api/v1/industrial/score` (filtros por período, máquina y línea).
9. **Frontend** (según la guía): página Producción (OEE con sus 3 componentes, downtime por
   máquina, ranking de máquinas) y página Costos (costo por unidad vs. estándar, composición
   de costos). El menú "Producción" se habilita cuando la empresa tiene datos industriales.

## Criterios de aceptación

- [ ] Con `sample-data/industrial/` se detectan los 3 problemas de la especificación (Low OEE,
      High Scrap, High Downtime), cada uno con su impacto.
- [ ] Hay tests de todas las fórmulas industriales.
- [ ] Las pantallas cumplen la guía de diseño en móvil y escritorio.

## Documentación a actualizar

- `03_modelo_datos.md`, `05_motores_analiticos.md` (sección industrial), `04_api.md`,
  `07_frontend.md`, `10_guia_usuario.md` y `13_bitacora.md`.
