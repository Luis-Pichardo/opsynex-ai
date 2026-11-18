# Etapa 07 — Motor de impacto financiero y Score Opsynex

**Depende de:** 06 · **Rama:** `etapa-07-impacto-y-score`

## Prompt

> Implementa en C# el motor de impacto financiero (secciones 8.5 y 12) y el Score
> Industrial-Financiero Opsynex (sección 13). Cada problema debe tener una pérdida económica
> estimada y la empresa un score de 0 a 100 por año, con componentes e interpretación. Al
> terminar esta etapa, el backend del MVP está completo.

## Tareas

1. **Impacto** (`Opsynex.Domain/Engines/ImpactEngine.cs`), por categoría:
   | Categoría | Cálculo estimado |
   |---|---|
   | MarginLoss | (margen del año anterior − margen actual) × Revenue actual |
   | ExcessCost | (crecimiento del costo − crecimiento del revenue) × costo del año anterior; o exceso sobre el benchmark × Revenue |
   | ProductivityLoss | (revenue/empleado del año anterior − actual) × empleados actuales |
   | OperationalWaste | material loss o scrap valorizado, o exceso de energía sobre el año base |
   Devuelve `EstimatedLoss`, `AffectedMarginPp`, `DeteriorationMagnitude` (%) y
   `SeverityLevel`. Nunca es negativo; si no se puede calcular, devuelve `null` con el motivo.
   El resultado se guarda en `Problem.ImpactValue` y `Problem.Evidence`.
2. **Ranking** por `ImpactValue` dentro de cada severidad. `GET /api/v1/impact/summary?year=`:
   pérdida total estimada, desglose por categoría y top 5.
3. **Score** (`ScoreEngine.cs`):
   | Componente | Peso | KPIs base (normalizados de 0 a 100) |
   |---|---|---|
   | Rentabilidad | 30 % | EbitdaMargin, GrossMargin |
   | Eficiencia de costos | 25 % | CostEfficiency, OperatingCostRatio |
   | Productividad | 25 % | RevenuePerEmployee y su tendencia |
   | Estabilidad financiera | 20 % | CurrentRatio, DebtToEquity, volatilidad de márgenes (y OperatingCashFlow/Revenue si existe; el archivo del cliente no trae flujo de efectivo) |
   Si falta un componente, su peso se redistribuye proporcionalmente y se indica.
   Interpretación: 80–100 operación saludable · 60–79 oportunidades de mejora · menos de 60
   deterioro operacional significativo.
4. `AnalysisPipeline` completo: KPIs → diagnóstico → impacto → score, en una transacción.
5. `GET /api/v1/score?year=`: score con componentes, pesos usados e interpretación; sin año devuelve la serie.
6. **Recomendación base con datos**: cada regla completa su texto con cifras concretas
   (por ejemplo: "El overhead creció 18 % frente a 4 % de los ingresos; revise la estructura
   administrativa…"). En la etapa 11 la IA la enriquece.

## Criterios de aceptación

- [ ] Tests de cada fórmula de impacto y del score (pesos, redistribución e interpretación).
- [ ] Con los libros sintéticos, cada problema tiene un impacto mayor que 0 y el score está
      entre 0 y 100.
- [ ] Test de integración de punta a punta: subir → homologar → consolidar → `GET /kpis`,
      `/problems`, `/score` e `/impact/summary` devuelven datos coherentes.
- [ ] Se cumplen los puntos de backend de la "Definición MVP" (sección 20).

## Documentación a actualizar

- `05_motores_analiticos.md`: "Impacto financiero" y "Score Opsynex", con fórmulas y un ejemplo numérico.
- `04_api.md`: `/score` e `/impact/summary`.
- `09_pruebas.md`: estrategia de pruebas y cobertura actual.
- `13_bitacora.md`.
