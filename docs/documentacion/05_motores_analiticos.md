## Motores analíticos

### Ingesta y validación

La ingesta acepta el **libro contable real de los clientes** (`.xls` de Excel 97-2003 o `.xlsx`,
unas 20 hojas con estados comparativos de 2 años, notas, nómina, ventas mensuales y balance de
comprobación; ver `docs/especificaciones/FORMATO_EXCEL_ENTRADA.md`) y, como alternativa, una
**plantilla simple** `.xlsx` o `.csv`. Está en `Opsynex.Infrastructure/Parsing` y se compone de:

| Paso | Componente | Qué hace |
|---|---|---|
| 1 | `WorkbookReader` | Valida la firma real (OLE2 `D0 CF 11 E0` para `.xls`, ZIP `50 4B` para `.xlsx`, texto para `.csv`) y el tamaño (`Upload:MaxMb`, 20 MB). Lee `.xls` y `.xlsx` con **ExcelDataReader** (valores calculados, nunca fórmulas; página de códigos 1252 registrada) y `.csv` con **CsvHelper** (UTF-8 o Windows-1252, separador detectado). Límite de hojas (60) y de filas por hoja (5 000). |
| 2 | `SheetClassifier` | Clasifica cada hoja **por su contenido**, no por su nombre (ver tabla siguiente). |
| 3 | `FinancialStatementExtractor` | Extrae el balance, el estado de resultados, las notas y las plantillas con el mismo algoritmo. |
| 4 | `EmployeeRosterExtractor`, `MonthlySalesExtractor`, `TrialBalanceExtractor` | Nómina (solo conteos), ventas mensuales y balance de comprobación. |
| 5 | `WorkbookQualityValidator` | Validaciones de calidad: todas son **advertencias** con hoja, fila y columna. |
| 6 | `UploadService` (Application) | Guarda el reporte de la carga, los valores en bruto, la serie mensual y los datos operacionales. |

#### Clasificación de hojas

Se normalizan (minúsculas y sin acentos) los textos de las primeras 15 filas:

| Tipo | Se reconoce por |
|---|---|
| `BalanceSheet` | "balance general", "balances generales", "estado de situación" |
| `IncomeStatement` | "estado de ganancias", "estado de resultados", "ganancias y pérdidas" |
| `EquityStatement` | "cambios en el patrimonio", "cambios en el capital" (se usa solo para validar) |
| `Notes` | "notas a los estados financieros" |
| `EmployeeRoster` | encabezados "código", "nombre", "cédula" y "cargo", "sueldo" o los meses |
| `MonthlySales` | columna "mes" con al menos 3 meses debajo y una columna de ventas o ingresos |
| `TrialBalance` | "balance de comprobación" y una columna "cuenta" con códigos numéricos |
| `CashFlow`, `CostStructure`, `Operational` | títulos de las plantillas ("flujo de efectivo", "estructura de costos", "datos operacionales") |
| `Unknown` | todo lo demás (hojas fiscales y de trabajo) o la hoja vacía: se informa como **ignorada** con su motivo |

En un CSV sin título, el tipo lo indica el usuario (`fileType`).

#### Algoritmo de extracción de estados

1. **Fila de años:** la primera de las primeras 20 filas con 1 a 4 celdas enteras entre 1990 y el
   año actual + 1, sin otros números. Esas columnas son las columnas de valores (pueden estar en
   E/G, F/H, G/I, L/N…).
2. **Etiqueta:** el texto más a la derecha antes de la primera columna de valores. Una fila sin
   valores es un **título**; si está en la columna A, abre una **sección** (por ejemplo
   "ACTIVOS CORRIENTES" o "4. CUENTAS POR COBRAR").
3. **Normalización:** se quitan "(nota N)", "(N.N)", "nota N", "Menos:" y "más:" y los espacios
   extra; se conservan la etiqueta original y la normalizada.
4. **Clase de fila:** `Detail` (cuenta), `Subtotal` (empieza con "Total" o no tiene etiqueta pero
   sí valores) o `Total` (gran total: "Total Activos", "Total Pasivos & Capital"). Los subtotales
   y totales se guardan para validar y **no** se suman como cuentas.
5. **Valores:** números de Excel o texto numérico (`1,234.56`, `(1,234)`, `RD$ 1,234`,
   `1.234,56`). Una celda vacía es "sin dato", no cero; un guion solo ("-") se lee como 0.
6. Cada fila con valores se guarda como `RawAccountValue` (hoja, tipo, fila, sección, etiquetas,
   año, valor y clase).

Extractores especiales:

- **Nómina:** cuenta las filas con código de empleado (por ejemplo "001") en total y por
  departamento ("DEPARTAMENTO: PRODUCCIÓN") y suma los sueldos de los meses. **Nunca** lee ni
  guarda nombres, cédulas, cargos ni sueldos individuales. El conteo llena
  `OperationalData.EmployeeCount` del año de la nómina.
- **Ventas mensuales:** columnas de ventas y compras (sin las de ITBIS) por mes →
  `MonthlyFinancialData`.
- **Balance de comprobación:** código jerárquico, descripción y columnas por naturaleza (activo,
  pasivo y capital, ingresos, costos y gastos). Las cuentas de último nivel se guardan como
  `Detail` con su código y las de agrupación como `Subtotal`.

#### Validaciones de calidad

Tolerancia de cuadre: **1 unidad o 0,01 % del valor**, lo que sea mayor.

| Código | Validación |
|---|---|
| `quality.balance_mismatch` | Total de activos ≠ total de pasivos y capital (por año). |
| `quality.note_mismatch` | Una línea con "(nota N)" no coincide con el total de la nota N (o la fila de la nota con la misma etiqueta). |
| `quality.equity_mismatch` | El estado de cambios en el patrimonio no refleja la ganancia del período o su saldo final ≠ total de capital del balance. |
| `quality.trial_balance_unbalanced` | La "prueba cero" del balance de comprobación ≠ 0 (activo + costos y gastos − pasivo y capital − ingresos, con las cuentas de último nivel). |
| `quality.trial_balance_negative_balance` | Saldos negativos de bancos, caja o inventarios en el balance de comprobación. |
| `quality.trial_balance_sales_mismatch` | Las ventas del balance de comprobación difieren de las del estado de resultados. |
| `quality.company_name_mismatch` | El nombre de la empresa (primera fila) difiere entre hojas. El nombre no se guarda: el mensaje solo nombra las hojas. |
| `quality.subtotal_mismatch` | Un subtotal no es la suma de sus líneas (con "Menos:" restando), ni el subtotal anterior más sus líneas, ni la suma de los subtotales de la sección. |
| `quality.missing_year` | El balance y el estado de resultados no traen los mismos años. |

Si alguna de las tres validaciones del balance de comprobación falla, se marca como **no
confiable** (`summary.trialBalanceReliable = false`) y los estados financieros quedan como fuente
de verdad.

**Errores bloqueantes** (la carga queda `Failed` y responde 422): archivo vacío
(`upload.empty_file`), firma inválida (`upload.invalid_signature`), ilegible o dañado
(`upload.unreadable`), sin balance ni estado de resultados (`upload.no_financial_statements`),
estados sin una fila de años válida (`upload.years_not_found`) o CSV sin tipo
(`upload.file_type_required`).

#### Datos de prueba sintéticos

`backend/tests/TestData/generate_sample_workbooks.py` genera en `backend/sample-data/` libros con
la misma estructura que los del cliente y **valores inventados** (empresa ficticia, "EMPLEADO 001",
cédulas "000-0000000-0"): el libro válido de 2 años (`.xls` y `.xlsx`), uno de 3 años con márgenes
en deterioro, una variante por cada inconsistencia, archivos inválidos y las plantillas simples.
Las fórmulas se calculan con LibreOffice y los valores esperados quedan en
`expected_values.json` (los leen las pruebas) y en el `README.md` de esa carpeta.

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
