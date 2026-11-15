# Etapa 07 — Motor de impacto financiero y Score Opsynex

**Depende de:** 06 · **Rama:** `etapa-07-impacto-y-score`

## Prompt

> Implementa el motor de impacto financiero (secciones 8.5 y 12) y el Score
> Industrial-Financiero Opsynex (sección 13). Cada problema debe tener una pérdida económica
> estimada y la empresa un score 0–100 por año con sus componentes e interpretación.
> Al terminar esta etapa el backend del MVP está completo.

## Tareas

1. **Impacto financiero** `app/engines/impact.py` — por categoría:
   | Categoría | Cálculo estimado |
   |---|---|
   | Margin Loss | (margen año anterior − margen actual) × Revenue actual |
   | Excess Cost | (crecimiento costo − crecimiento revenue) × costo año anterior; o exceso sobre benchmark × Revenue |
   | Productivity Loss | (revenue/empleado año anterior − actual) × empleados actuales |
   | Operational Waste | material_loss / scrap valorizado, o exceso de energía sobre año base |
   Devuelve `estimated_loss`, `affected_margin_pp`, `deterioration_magnitude` (%) y
   `severity_level`. El impacto nunca es negativo; si no se puede calcular, `None` con motivo.
   Guarda los resultados en `problems.impact_value` y `problems.evidence`.
2. **Ranking**: reordena problemas por `impact_value` dentro de cada severidad. Endpoint
   `GET /api/v1/impact/summary?year=` con pérdida total estimada, por categoría y top 5.
3. **Score Opsynex** `app/engines/score.py`:
   | Componente | Peso | KPIs base (normalizados 0–100) |
   |---|---|---|
   | Rentabilidad | 30 % | ebitda_margin, gross_margin |
   | Eficiencia de costos | 25 % | cost_efficiency, operating_cost_ratio |
   | Productividad | 25 % | revenue_per_employee (y su tendencia) |
   | Estabilidad financiera | 20 % | liabilities/equity, operating cash flow / revenue, volatilidad de márgenes |
   Si falta un componente, redistribuye su peso proporcionalmente e indícalo.
   Interpretación: 80–100 Operación saludable · 60–79 Oportunidades de mejora ·
   < 60 Deterioro operacional significativo.
4. Integra impacto y score en `run_analysis` (pipeline completo: KPIs → diagnóstico → impacto → score).
5. `GET /api/v1/score?year=` (score con componentes, pesos usados e interpretación; sin año devuelve la serie).
6. **Recomendaciones**: cada regla tiene un texto base; complétalo con datos concretos
   (ej. "El overhead creció 18 % vs. 4 % de los ingresos; revise la estructura administrativa…").

## Criterios de aceptación

- [ ] Tests de cada fórmula de impacto y del score (pesos, redistribución, interpretación).
- [ ] Con `sample_data/`: cada problema tiene impacto > 0 y el score está entre 0 y 100.
- [ ] Prueba de integración: subir archivos → homologar → consolidar → `GET /kpis`,
      `/problems`, `/score`, `/impact/summary` devuelven datos coherentes.
- [ ] Se cumplen los puntos de backend de la "Definición MVP" (sección 20).

## Documentación a actualizar

- `05_motores_analiticos.md`: secciones "Impacto financiero" y "Score Opsynex" con fórmulas y un ejemplo numérico.
- `04_api.md`: `/score`, `/impact/summary`.
- `08_pruebas.md`: estrategia de pruebas y cobertura actual.
- `12_bitacora.md`.
