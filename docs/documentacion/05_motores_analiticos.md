## Motores analíticos

### Ingesta y validación

> Pendiente (Etapa 03).

### Homologación contable

Distintas organizaciones usan estructuras contables y nombres de cuentas diferentes. La capa
de homologación mapea las cuentas del cliente a categorías estandarizadas de Opsynex.

| Cuenta del cliente | Categoría Opsynex |
|---|---|
| Gasto Energía Planta | `energy_cost` |
| Nómina Indirecta | `overhead_cost` |
| Desperdicio Materia Prima | `material_loss` |
| Gasto Mantenimiento | `maintenance_cost` |

> Pendiente (Etapa 04): algoritmo y catálogo completo.

### Motor de KPIs

| KPI | Fórmula | Mide |
|---|---|---|
| EBITDA Margin | EBITDA / Revenue | Rentabilidad operacional |
| Margen bruto | (Revenue − COGS) / Revenue | Rentabilidad de producción o servicios |
| Cost Efficiency Ratio | Total Cost / Revenue | Proporción del costo total sobre ingresos |
| Revenue per Employee | Revenue / Number of Employees | Productividad laboral |
| Operating Cost Ratio | Operating Expenses / Revenue | Carga de gastos operacionales |

> Pendiente (Etapa 05): tendencias, normalización y benchmarks.

### Motor de diagnóstico

| Condición | Diagnóstico |
|---|---|
| Disminuye EBITDA margin | Deterioro de rentabilidad |
| Aumentan costos con ingresos estables | Ineficiencia operacional |
| Crecimiento del overhead superior al de los ingresos | Ineficiencia administrativa |
| Disminuye revenue per employee | Deterioro de productividad |
| Se deteriora el margen bruto | Problema de absorción de costos |

Niveles de prioridad: **High** (impacto financiero crítico), **Medium** (ineficiencia
moderada), **Low** (recomendación de monitoreo).

> Pendiente (Etapa 06): umbrales, reglas adicionales y priorización.

### Motor de impacto financiero

Categorías: Margin Loss (reducción de rentabilidad), Excess Cost (ineficiencia de la estructura
de costos), Productivity Loss (reducción de productividad operacional) y Operational Waste
(ineficiencia de recursos). Estima pérdida económica, margen afectado, magnitud del deterioro y
nivel de severidad.

> Pendiente (Etapa 07): fórmulas y ejemplo numérico.

### Score Opsynex

| Componente | Peso |
|---|---|
| Rentabilidad | 30 % |
| Eficiencia de costos | 25 % |
| Productividad | 25 % |
| Estabilidad financiera | 20 % |

| Score | Interpretación |
|---|---|
| 80–100 | Operación saludable |
| 60–79 | Oportunidades de mejora |
| Menor a 60 | Deterioro operacional significativo |

> Pendiente (Etapa 07): cálculo detallado.

### Módulo industrial

> Pendiente (Etapa 11): OEE, scrap rate, downtime, cost per unit e impacto industrial.
