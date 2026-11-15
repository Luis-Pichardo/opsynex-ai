# Etapa 05 — Motor de cálculo de KPIs

**Depende de:** 04 · **Rama:** `etapa-05-motor-kpis`

## Prompt

> Implementa el motor de KPIs (secciones 8.3 y 9 de las especificaciones): cálculo automático,
> almacenamiento histórico, normalización y evaluación de tendencias. Se debe ejecutar
> automáticamente al consolidar datos y bajo demanda.

## Tareas

1. `app/engines/kpis.py` (funciones puras, entrada = datos financieros + operacionales de un año):
   | KPI | Fórmula |
   |---|---|
   | `ebitda_margin` | EBITDA / Revenue |
   | `gross_margin` | (Revenue − COGS) / Revenue |
   | `cost_efficiency` | Total Cost / Revenue |
   | `revenue_per_employee` | Revenue / Number of Employees |
   | `operating_cost_ratio` | Operating Expenses / Revenue |
   | `overhead_ratio` | Overhead Cost / Revenue (auxiliar para diagnóstico) |
   | `energy_cost_ratio` | Energy Cost / Revenue (auxiliar) |
   | `cost_per_unit` | Total Cost / Units Produced (si hay datos operacionales) |
   Devuelven `None` si falta un dato o el denominador es 0.
2. **Tendencias**: para cada KPI, variación absoluta y porcentual año contra año, dirección
   (`up`/`down`/`stable`, umbral configurable ±1 %) y si la dirección es favorable
   (p. ej. bajar `cost_efficiency` es favorable). También crecimiento de revenue, total_cost y
   overhead año a año (lo usa el diagnóstico).
3. **Normalización**: convierte cada KPI a escala 0–100 con rangos de referencia configurables
   por industria (`manufacturing`, `services`) en `app/engines/config/benchmarks.yaml`
   (ej. EBITDA margin manufactura: 0 % → 0 pts, 20 % → 100 pts). Lo usa el score (Etapa 07).
4. Agrega columnas a `kpi_results` si hacen falta (`operating_cost_ratio`, `overhead_ratio`,
   `cost_per_unit`, `trends` JSON, `normalized` JSON) con migración.
5. `app/services/analysis_pipeline.py`: orquestador `run_analysis(company_id)` que recalcula
   KPIs de todos los años (y en etapas siguientes diagnóstico, impacto y score). Se llama al
   consolidar y desde `POST /api/v1/analysis/run`.
6. `GET /api/v1/kpis?year=` — KPIs por año con tendencias; sin `year` devuelve la serie histórica.

## Criterios de aceptación

- [ ] Tests unitarios de cada fórmula, incluidos casos borde (cero, None, negativos).
- [ ] Con `sample_data/`, los KPIs coinciden con valores calculados a mano en el test.
- [ ] `GET /kpis` devuelve la serie 2022–2024 con tendencias.

## Documentación a actualizar

- `05_motores_analiticos.md`: sección "Motor de KPIs" (tabla de fórmulas, interpretación,
  tendencias, normalización y benchmarks).
- `04_api.md`: `/kpis`, `/analysis/run`.
- `12_bitacora.md`.
