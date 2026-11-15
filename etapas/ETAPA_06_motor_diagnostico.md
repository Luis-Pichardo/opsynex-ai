# Etapa 06 — Motor de diagnóstico

**Depende de:** 05 · **Rama:** `etapa-06-motor-diagnostico`

## Prompt

> Implementa el motor de diagnóstico (secciones 8.4 y 11 de las especificaciones): un motor de
> reglas configurable que evalúa KPIs y tendencias, detecta anomalías e ineficiencias, genera
> problemas con severidad y recomendación, y los prioriza.

## Tareas

1. **Motor de reglas** `app/engines/diagnosis/`: cada regla es un objeto con `code`, `title`,
   `category`, `condition(context) -> bool`, `severity(context) -> high|medium|low`,
   `impact_category`, `evidence(context)` y `recommendation`. Umbrales en
   `app/engines/config/rules.yaml` para poder ajustarlos sin tocar código.
2. **Reglas mínimas** (de la sección 11):
   | Código | Condición | Diagnóstico | Impacto |
   |---|---|---|---|
   | `EBITDA_DECLINE` | Disminuye EBITDA margin | Deterioro de rentabilidad | margin_loss |
   | `COST_GROWTH_FLAT_REVENUE` | Costos totales suben y revenue estable (±2 %) | Ineficiencia operacional | excess_cost |
   | `OVERHEAD_OUTPACES_REVENUE` | Crecimiento overhead > crecimiento revenue | Ineficiencia administrativa | excess_cost |
   | `PRODUCTIVITY_DECLINE` | Disminuye revenue per employee | Deterioro de productividad | productivity_loss |
   | `GROSS_MARGIN_DECLINE` | Deteriora margen bruto | Problema de absorción de costos | margin_loss |
   Reglas adicionales recomendadas:
   | `LOW_EBITDA_MARGIN` | EBITDA margin bajo benchmark de la industria | Rentabilidad baja | margin_loss |
   | `HIGH_OPEX_RATIO` | Operating cost ratio sobre benchmark | Estructura de gastos pesada | excess_cost |
   | `ENERGY_COST_SPIKE` | Energy cost ratio crece > 10 % | Consumo energético ineficiente | operational_waste |
   | `HIGH_MATERIAL_LOSS` | material_loss o scrap / COGS > umbral | Desperdicio de materiales | operational_waste |
3. **Severidad** según magnitud del deterioro (ej. caída de EBITDA margin > 5 pp → high,
   2–5 pp → medium, < 2 pp → low). Niveles: High = impacto financiero crítico,
   Medium = ineficiencia moderada, Low = recomendación de monitoreo.
4. **Priorización**: orden por severidad y luego por impacto económico (el impacto se llena en
   la Etapa 07; deja el campo preparado).
5. Integra el motor en `run_analysis`: reemplaza los problemas `open` del año analizado de
   forma idempotente (sin duplicar), conservando el `status` si el usuario ya los marcó.
6. Endpoints: `GET /api/v1/problems` (filtros: year, severity, category, status; orden por
   prioridad), `GET /api/v1/problems/{id}` (incluye evidencia e historial del KPI asociado),
   `PATCH /api/v1/problems/{id}` (cambiar status; Admin/Analyst).

## Criterios de aceptación

- [ ] Cada regla tiene tests que la disparan y que no la disparan.
- [ ] Con `sample_data/` se detectan **al menos 3 tipos de problemas** distintos.
- [ ] Ejecutar el análisis dos veces no duplica problemas.

## Documentación a actualizar

- `05_motores_analiticos.md`: sección "Motor de diagnóstico" (tabla de reglas, umbrales,
  severidad, priorización, cómo agregar una regla nueva).
- `04_api.md`: endpoints `/problems`.
- `12_bitacora.md`.
