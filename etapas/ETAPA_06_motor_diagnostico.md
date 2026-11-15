# Etapa 06 — Motor de diagnóstico

**Depende de:** 05 · **Rama:** `etapa-06-motor-diagnostico`

## Prompt

> Implementa el motor de diagnóstico en C# (secciones 8.4 y 11 de las especificaciones): un
> motor de reglas configurable que evalúa KPIs y tendencias, detecta ineficiencias, genera
> problemas con severidad y recomendación base, y los prioriza.

## Tareas

1. **Motor de reglas** en `Opsynex.Domain/Engines/Diagnosis/`: interfaz `IDiagnosticRule` con
   `Code`, `Title`, `Category`, `ImpactCategory`, `Evaluate(DiagnosisContext) → RuleResult?`
   (incluye severidad, evidencia y recomendación). Las reglas se registran por DI. Umbrales en
   `appsettings` (sección `DiagnosisRules`) para ajustarlos sin tocar código.
2. **Reglas mínimas** (sección 11):
   | Código | Condición | Diagnóstico | Impacto |
   |---|---|---|---|
   | `EBITDA_DECLINE` | Disminuye el EBITDA margin | Deterioro de rentabilidad | MarginLoss |
   | `COST_GROWTH_FLAT_REVENUE` | Los costos totales suben y el revenue está estable (±2 %) | Ineficiencia operacional | ExcessCost |
   | `OVERHEAD_OUTPACES_REVENUE` | El overhead crece más que el revenue | Ineficiencia administrativa | ExcessCost |
   | `PRODUCTIVITY_DECLINE` | Disminuye el revenue per employee | Deterioro de productividad | ProductivityLoss |
   | `GROSS_MARGIN_DECLINE` | Se deteriora el margen bruto | Problema de absorción de costos | MarginLoss |
   Adicionales recomendadas:
   | `LOW_EBITDA_MARGIN` | EBITDA margin bajo el benchmark de la industria | Rentabilidad baja | MarginLoss |
   | `HIGH_OPEX_RATIO` | Operating cost ratio sobre el benchmark | Estructura de gastos pesada | ExcessCost |
   | `ENERGY_COST_SPIKE` | El energy cost ratio crece más de 10 % | Consumo energético ineficiente | OperationalWaste |
   | `HIGH_MATERIAL_LOSS` | Material loss / COGS sobre el umbral | Desperdicio de materiales | OperationalWaste |
3. **Severidad** según la magnitud (por ejemplo, caída del EBITDA margin de más de 5 pp →
   High, de 2 a 5 pp → Medium, de menos de 2 pp → Low). High = impacto financiero crítico,
   Medium = ineficiencia moderada, Low = recomendación de monitoreo.
4. **Priorización**: por severidad y luego por impacto económico (el impacto se llena en la
   etapa 07; deja el ordenamiento preparado).
5. Integración en `AnalysisPipeline`: reemplaza los problemas del año de forma **idempotente**
   (sin duplicar) y conserva el `Status` si el usuario ya cambió alguno.
6. `ProblemsController`: `GET /api/v1/problems` (filtros year, severity, category y status;
   paginado; orden por prioridad), `GET /api/v1/problems/{id}` (con evidencia e historial del
   KPI relacionado) y `PATCH /api/v1/problems/{id}` (cambio de estado; Admin y Analyst).

## Criterios de aceptación

- [ ] Cada regla tiene tests que la disparan y tests que no la disparan.
- [ ] Con `sample-data/` se detectan **al menos 3 tipos de problemas** distintos.
- [ ] Ejecutar el análisis dos veces no duplica problemas.

## Documentación a actualizar

- `05_motores_analiticos.md`: "Motor de diagnóstico" (reglas, umbrales, severidad, priorización
  y cómo agregar una regla nueva).
- `04_api.md`: `/problems`.
- `13_bitacora.md`.
