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

El proceso es **automático** al subir un archivo y la revisión del analista es **opcional**
(`FLUJO_DE_USUARIO.md`). Lo reparten dos componentes:

- El **servicio de IA** (Python) genera las **sugerencias**: diccionario, palabras clave y
  coincidencia difusa (ver el capítulo *Servicio de inteligencia artificial*).
- El **API .NET** guarda los mapeos, aplica los confirmados, consolida en `FinancialData` y marca
  la carga como "revisión recomendada" cuando hay cuentas dudosas.

#### Qué se homologa y con qué rol

| Hoja | Alcance del mapeo | Rol | Alimenta |
|---|---|---|---|
| Balance general, estado de resultados, plantillas de flujo y de costos | `Statement` | `Primary` | Los totales: ingresos, costos, gastos, utilidad, activos, pasivos, patrimonio… |
| Notas | `Notes` | `Breakdown` | Solo las categorías analíticas: energía, mantenimiento, personal, materias primas, gastos generales y merma (más la depreciación incluida en el costo) |
| Balance de comprobación | — | — | No se homologa: solo valida |
| Nómina y plantilla operacional | — | — | Van directo a `OperationalData` |

Cada cuenta se identifica por **alcance + sección + etiqueta normalizada**. La sección solo se
usa en los subtotales de los estados, para separar "Total corriente" de activos y de pasivos.
"Energía Eléctrica" en la nota 13 y en la nota 12 es la misma cuenta. Los mapeos son únicos por
empresa con esa clave y **se reutilizan** en las cargas siguientes.

#### Algoritmo

1. Para cada cuenta distinta de la carga se usa el **mapeo confirmado** de la empresa, si existe.
2. Para las demás se piden sugerencias al servicio de IA, enviando los mapeos confirmados como
   `known_mappings`. Se aplica la mejor sugerencia sin esperar confirmación: el mapeo queda
   guardado como **no confirmado**, con su confianza y su método (`Exact`, `Keyword`, `Fuzzy`,
   `Rule`…).
3. **Fallback:** si el servicio de IA no responde (timeout de 10 s, reintentos y circuit
   breaker), el API usa un diccionario mínimo local con coincidencia exacta de las etiquetas más
   comunes (`Method = Fallback`). Lo que no reconoce queda en `Other` con confianza 0
   (`Unmatched`).
4. **Consolidación** (motor puro `ConsolidationEngine`), por categoría y año:
   - Categorías de los estados: solo filas `Primary`.
   - Categorías analíticas: filas `Breakdown` de las notas (si no hay, las `Primary`).
   - Depreciación: la de gastos más la incluida en el costo de producción.
   - En cada grupo, **si hay subtotales mapeados a la categoría se usan ellos; si no, se suman los
     detalles**. Nunca ambos: así "Total compensaciones al personal" no se suma con sus líneas.
   - `Other` no se consolida.
5. **Derivados:** `Revenue = GrossSales − SalesDiscounts` (si no viene),
   `TotalCost = Cogs + OperatingExpenses`,
   `Ebitda = EarningsBeforeTax + FinancialExpenses + Depreciation`,
   `EarningsBeforeTax = NetIncome + IncomeTax` (si no viene) y
   `TotalLiabilities = TotalLiabilitiesAndEquity − Equity`.
6. **Upsert en `FinancialData`** de todos los años del archivo. Solo se sobrescriben los grupos de
   campos que trae la carga (resultados, balance o flujo), para que una plantilla de un solo
   estado no borre el otro.
7. **Revisión recomendada (`NeedsReview`):** se activa si alguna cuenta `Primary` sin confirmar
   tiene confianza < 0,8 o quedó sin categoría. No bloquea el análisis. Las cuentas sin categoría
   se informan como advertencia `mapping.unmapped_account` con su monto, para ver si son
   materiales.

La carga queda `Processed` con `analysisStatus = Completed`. Si el análisis falla, los datos
quedan guardados con `analysisStatus = Failed` y la advertencia `analysis.failed`.

#### Mapeo del libro dominicano

Con el libro sintético, todas las líneas de la sección 4 de la especificación reciben la
categoría correcta con confianza ≥ 0,8 (77 de 77 en las pruebas). Algunos ejemplos:

| Hoja → etiqueta | Categoría | Rol |
|---|---|---|
| g&P → Ventas netas | `GrossSales` | Primary |
| g&P → Descuentos sobre ventas | `SalesDiscounts` | Primary |
| g&P → Compensaciones al personal / Gastos Administrativos / Total gastos de operaciones | `OperatingExpenses` | Primary |
| g&P → Gastos Financieros | `FinancialExpenses` | Primary |
| g&P → Ingresos netos antes impuesto s/renta | `EarningsBeforeTax` | Primary |
| BALANCE → Total corriente (activos / pasivos) | `CurrentAssets` / `CurrentLiabilities` | Primary |
| BALANCE → Total Pasivos & Capital | `TotalLiabilitiesAndEquity` | Primary |
| nota3 y nota4 → Energía Eléctrica | `EnergyCost` | Breakdown |
| nota3 → Total compensaciones al personal; nota4 → Total labor directa | `LaborCost` | Breakdown |
| nota4 → Materias primas importadas, Compras de materias primas locales | `RawMaterials` | Breakdown |
| nota4 → Depreciación Maquinarias & Equipos | `Depreciation` | Breakdown |
| nota3 → Total gastos generales y administrativos | `OverheadCost` | Breakdown |

### Motor de KPIs

Clases puras en `Opsynex.Domain/Engines/Kpis`, sin EF ni HTTP. El `AnalysisPipeline` recalcula
los KPIs de **todos los años** de la empresa cada vez que se consolida una carga, y también bajo
demanda (`POST /api/v1/analysis/run`). Los resultados se guardan en `KpiResults`, uno por año.

#### Fórmulas

| KPI | Nombre | Fórmula | Unidad | Mejor si | Dimensión del score |
|---|---|---|---|---|---|
| `EbitdaMargin` | Margen EBITDA | EBITDA / Revenue | proporción | sube | Rentabilidad |
| `GrossMargin` | Margen bruto | (Revenue − COGS) / Revenue | proporción | sube | Rentabilidad |
| `NetMargin` | Margen neto | Net Income / Revenue | proporción | sube | Rentabilidad |
| `CostEfficiency` | Eficiencia de costos | Total Cost / Revenue | proporción | baja | Eficiencia de costos |
| `OperatingCostRatio` | Gastos operacionales / ingresos | Operating Expenses / Revenue | proporción | baja | Eficiencia de costos |
| `OverheadRatio` | Gastos generales / ingresos | Overhead Cost / Revenue | proporción | baja | Eficiencia de costos |
| `EnergyCostRatio` | Energía / ingresos | Energy Cost / Revenue | proporción | baja | Eficiencia de costos |
| `LaborCostRatio` | Costo de personal / ingresos | Labor Cost / Revenue | proporción | baja | Eficiencia de costos |
| `InventoryDays` | Días de inventario | Inventory / COGS × 365 | días | baja | Eficiencia de costos |
| `RevenuePerEmployee` | Ingresos por empleado | Revenue / Number of Employees | monto | sube | Productividad |
| `CostPerUnit` | Costo por unidad | Total Cost / Units Produced | monto | baja | Productividad |
| `CurrentRatio` | Razón corriente | Current Assets / Current Liabilities | veces | sube | Estabilidad financiera |
| `DebtToEquity` | Endeudamiento | Total Liabilities / Equity | veces | baja | Estabilidad financiera |
| `DaysSalesOutstanding` | Días de cobro | Accounts Receivable / Revenue × 365 | días | baja | Estabilidad financiera |

Reglas:

- **Divisiones protegidas:** el KPI es `null` si falta un dato o si el denominador es 0 o
  negativo. Ingresos, costo de ventas, empleados, unidades, pasivos corrientes o patrimonio
  negativos no tienen una interpretación útil. Los numeradores negativos sí se aceptan (margen
  negativo, pérdida neta).
- **Redondeo:** ratios con 4 decimales; montos y días con 2 (al par, como en SQL Server).
- **Datos:** los financieros salen de `FinancialData`; los empleados y las unidades, de
  `OperationalData` del mismo año. Si un año no tiene nómina, los ingresos por empleado quedan
  en `null`.

Interpretación rápida: un margen EBITDA de 0,1861 es 18,61 %; una eficiencia de costos de
0,8533 significa que se gastan 85,33 centavos por cada peso de ingreso; una razón corriente de
2,77 indica que los activos corrientes cubren 2,77 veces los pasivos corrientes.

#### Tendencias (`TrendAnalyzer`)

Para cada KPI de un año frente al **año anterior** se calculan la variación absoluta, la variación
porcentual (sobre el valor absoluto del año anterior) y la **dirección**: `Up`, `Down` o
`Stable`. Es `Stable` si la variación porcentual está dentro de ±1 %
(`Benchmarks:StableThresholdPercent`). Una tendencia es **favorable** si va en el sentido bueno
del KPI: bajar la eficiencia de costos es favorable y bajar el margen no. Las estables no son ni
favorables ni desfavorables. También se calcula el crecimiento año a año de los ingresos, el
costo total y los gastos generales, que usa el diagnóstico (etapa 06). El primer año no tiene
tendencias.

#### Normalización (0–100) y benchmarks

`KpiNormalizer` convierte cada KPI en puntos con interpolación lineal entre un valor que vale 0
(`Worst`) y uno que vale 100 (`Best`), con tope en 0 y 100. Para los KPIs donde bajar es mejor,
`Worst` es mayor que `Best`. Los rangos están en `appsettings.json`, sección `Benchmarks`
(opciones tipadas `BenchmarkOptions`, validadas al arrancar):

- **Por industria** (`Industries:Manufacturing`, `Industries:Services`): KPIs en proporción,
  veces o días. Por ejemplo, el margen EBITDA en manufactura va de 0 % → 0 pts a 20 % → 100 pts,
  y la eficiencia de costos de 1,00 → 0 pts a 0,75 → 100 pts.
- **Por moneda** (`Currencies:DOP`, `Currencies:USD`): KPIs en monto. Los ingresos por empleado
  en RD$ van de 1,5 M → 0 a 15 M → 100; en US$, de 30 000 → 0 a 300 000 → 100. El costo por unidad
  no tiene benchmark porque depende del producto.

Los rangos de referencia son configurables y conviene calibrarlos con el cliente. La
normalización la usa el score (etapa 07).

#### Serie mensual (`MonthlyAnalyzer`)

Con la hoja de ventas mensuales (`MonthlyFinancialData`) se calculan:

- El crecimiento contra el mes anterior (solo entre meses consecutivos).
- El promedio móvil de 3 meses.
- Un índice de estacionalidad: ventas del mes / promedio mensual de su año, donde 1 es un mes
  promedio.

Se devuelve en `GET /kpis` y lo usarán el dashboard y la detección de anomalías (etapa 11).

#### KPIs del libro sintético

Los valores esperados se calculan a mano (en Python, con `Decimal`) en el generador y se comparan
con los del motor en las pruebas:

| KPI | 2023 | 2024 |
|---|---:|---:|
| `CostEfficiency` | 0.842 | 0.8533 |
| `CostPerUnit` | — | — |
| `CurrentRatio` | 2.3823 | 2.7659 |
| `DaysSalesOutstanding` | 35.22 | 36.83 |
| `DebtToEquity` | 0.6042 | 0.474 |
| `EbitdaMargin` | 0.1975 | 0.1861 |
| `EnergyCostRatio` | 0.0582 | 0.0617 |
| `GrossMargin` | 0.2897 | 0.2805 |
| `InventoryDays` | 44.5 | 45.17 |
| `LaborCostRatio` | 0.1688 | 0.1723 |
| `NetMargin` | 0.1003 | 0.0946 |
| `OperatingCostRatio` | 0.1318 | 0.1338 |
| `OverheadRatio` | 0.0468 | 0.0469 |
| `RevenuePerEmployee` | — | 12116066.67 |


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

Motor de reglas configurable en `Opsynex.Domain/Engines/Diagnosis`, sin EF ni HTTP. Cada regla
implementa `IDiagnosticRule` con `Code`, `Title`, `Category`, `ImpactCategory` y
`Evaluate(DiagnosisContext) → RuleResult?`. `RuleResult` trae la severidad, la descripción con
cifras, la recomendación base, la evidencia en JSON y el KPI relacionado. El `AnalysisPipeline`
ejecuta el diagnóstico de **cada año** después de calcular los KPIs, comparando con el año
anterior cuando existe.

#### Reglas

Cada regla calcula una **magnitud**, que es positiva cuando hay problema. La severidad sale de
comparar esa magnitud con tres umbrales: ≥ `High` → High, ≥ `Medium` → Medium y ≥ `Low` →
Low. Por debajo de `Low` no hay problema.

| Código | Condición | Magnitud | Low / Medium / High | Diagnóstico | Impacto |
|---|---|---|---|---|---|
| `EBITDA_DECLINE` | Disminuye el margen EBITDA | caída en pp | 0,5 / 2 / 5 | Deterioro de rentabilidad | MarginLoss |
| `GROSS_MARGIN_DECLINE` | Se deteriora el margen bruto | caída en pp | 0,5 / 2 / 5 | Problema de absorción de costos | MarginLoss |
| `COST_GROWTH_FLAT_REVENUE` | El costo total sube con ingresos estables (±2 %) | crecimiento del costo (%) | 2 / 5 / 10 | Ineficiencia operacional | ExcessCost |
| `OVERHEAD_OUTPACES_REVENUE` | Los gastos generales crecen más que los ingresos | diferencia de crecimiento (pp) | > 0 / 5 / 10 | Ineficiencia administrativa | ExcessCost |
| `LABOR_COST_OUTPACES_REVENUE` | El costo de personal crece más que los ingresos | diferencia de crecimiento (pp) | > 0 / 5 / 10 | Ineficiencia en costos de personal | ExcessCost |
| `PRODUCTIVITY_DECLINE` | Bajan los ingresos por empleado | caída (%) | 2 / 7 / 15 | Deterioro de productividad | ProductivityLoss |
| `REVENUE_DECLINE` | Los ingresos caen | caída (%) | 2 / 7 / 15 | Contracción de ventas | MarginLoss |
| `ENERGY_COST_SPIKE` | Sube el costo de energía / ingresos | aumento relativo (%) | 10 / 15 / 25 | Consumo energético ineficiente | OperationalWaste |
| `LOW_EBITDA_MARGIN` | Margen EBITDA bajo la referencia (10 % manufactura, 12 % servicios) | pp por debajo | > 0 / 2 / 5 | Rentabilidad baja | MarginLoss |
| `HIGH_OPEX_RATIO` | Gastos operacionales / ingresos sobre la referencia (25 % manufactura, 40 % servicios) | pp por encima | > 0 / 5 / 10 | Estructura de gastos pesada | ExcessCost |
| `HIGH_MATERIAL_LOSS` | Pérdida de materiales / COGS sobre el 3 % | pp por encima | > 0 / 2 / 5 | Desperdicio de materiales | OperationalWaste |
| `LOW_LIQUIDITY` | Razón corriente menor que 1,0 | diferencia (veces) | > 0 / 0,1 / 0,3 | Riesgo de liquidez | FinancialAlert |
| `HIGH_LEVERAGE` | Pasivos / patrimonio mayor que 3,0 | diferencia (veces) | > 0 / 1 / 2 | Endeudamiento alto | FinancialAlert |

`FinancialAlert` (alerta financiera) es una categoría nueva para los problemas que no se
cuantifican como pérdida (liquidez, endeudamiento).

#### Umbrales configurables

Están en `appsettings.json`, sección `DiagnosisRules:Rules:<CÓDIGO>`, con `Enabled`, `Low`,
`Medium`, `High` y `Reference`. `Reference` es la referencia por industria o `Default`. Lo que no
esté configurado usa los valores por defecto de `DiagnosisSettings.Defaults`. El API valida al
arrancar que `0 ≤ Low ≤ Medium ≤ High`. Para desactivar una regla: `"Enabled": false`.

#### Priorización

Los problemas se ordenan por **severidad** (High primero) y luego por **impacto económico**
(mayor primero; los que no tienen impacto van al final). En la lista, `priority` es la posición
(1 = más urgente).

Cada recomendación base incluye las cifras del problema (por ejemplo: "El gasto general y
administrativo creció +7,58 % frente a +7,41 % de los ingresos; revisa la estructura
administrativa…") y termina con el impacto estimado. La IA la enriquece en la etapa 11.

#### Reemplazo idempotente

En cada análisis, para cada año:

- Los problemas que se vuelven a detectar se **actualizan** (severidad, textos y evidencia) y
  **conservan el estado** que puso el usuario (`Acknowledged`, `Resolved`).
- Los nuevos se agregan con estado `Open`.
- Los que ya no se detectan se eliminan.

El índice único (empresa, año, código) garantiza que nunca haya duplicados.

#### Hallazgos positivos

El motor también registra mejoras: el margen EBITDA o el bruto suben al menos 0,5 pp, el
endeudamiento baja, la liquidez mejora, la eficiencia de costos mejora o los ingresos crecen 5 %
o más. Se guardan por año en `KpiResults.Highlights` y se devuelven en `GET /problems`, para que
el resumen ejecutivo no muestre solo problemas.

#### Cómo agregar una regla

1. Crea una clase en `Opsynex.Domain/Engines/Diagnosis/Rules` que implemente `IDiagnosticRule`.
   Se registra sola por DI.
2. Agrega sus umbrales por defecto en `DiagnosisSettings.Defaults` (y en `appsettings.json` si se
   quieren ajustables).
3. Escribe tests que la disparen y que no la disparen (`tests/Opsynex.UnitTests/Diagnosis`).

#### Resultado con el libro sintético de 3 años

| Año | Problemas |
|---|---|
| 2023 | `EBITDA_DECLINE` (Medium, −3,05 pp), `GROSS_MARGIN_DECLINE` (Medium, −3,11 pp), `LABOR_COST_OUTPACES_REVENUE` (Low: 8,43 % frente a 8,21 %) |
| 2024 | `EBITDA_DECLINE` (Low, −1,14 pp), `GROSS_MARGIN_DECLINE` (Low, −0,92 pp), `OVERHEAD_OUTPACES_REVENUE` (Low: 7,58 % frente a 7,41 %), `LABOR_COST_OUTPACES_REVENUE` (Low: 9,66 % frente a 7,41 %) |

Hallazgos positivos de 2024: menor endeudamiento, mejor liquidez y crecimiento de ingresos.

### Motor de impacto financiero

`Opsynex.Domain/Engines/Impact/ImpactEngine.cs` es una clase pura. Para cada problema estima:

- **Pérdida económica estimada** (`EstimatedLoss`): monto anual en la moneda de la empresa.
- **Margen afectado** (`AffectedMarginPp`): pérdida / ingresos × 100, en puntos de margen.
- **Magnitud del deterioro** (`DeteriorationMagnitude`): cuánto empeoró el indicador base, en %.
- **Nivel de severidad** (`SeverityLevel`): el del diagnóstico.

La pérdida **nunca es negativa**. Si faltan datos, es `null` y `reason` dice por qué. El resultado
se guarda en `Problems.ImpactValue` y en `Problems.Evidence.impact` (con la fórmula y los insumos).

#### Fórmulas por categoría

| Categoría | Regla | Pérdida estimada |
|---|---|---|
| Margin Loss | `EBITDA_DECLINE`, `GROSS_MARGIN_DECLINE` | (margen anterior − margen actual) × ingresos actuales |
| Margin Loss | `LOW_EBITDA_MARGIN` | (margen de referencia − margen actual) × ingresos actuales |
| Margin Loss | `REVENUE_DECLINE` | (ingresos anteriores − actuales) × margen bruto anterior (o EBITDA si no hay bruto) |
| Excess Cost | `OVERHEAD_OUTPACES_REVENUE`, `LABOR_COST_OUTPACES_REVENUE`, `COST_GROWTH_FLAT_REVENUE` | (crecimiento del costo − crecimiento de los ingresos) × costo del año anterior |
| Excess Cost | `HIGH_OPEX_RATIO` | (gastos operacionales / ingresos − referencia) × ingresos |
| Productivity Loss | `PRODUCTIVITY_DECLINE` | (ingresos por empleado anteriores − actuales) × empleados actuales |
| Operational Waste | `HIGH_MATERIAL_LOSS` | (mermas / costo de ventas − umbral) × costo de ventas |
| Operational Waste | `ENERGY_COST_SPIKE` | (energía / ingresos actual − del año base) × ingresos actuales |
| Financial Alert | `LOW_LIQUIDITY`, `HIGH_LEVERAGE` | No se cuantifica: es un riesgo, no una pérdida operativa (sí se informa la magnitud) |

En las reglas contra una referencia (mermas, gastos, margen) se valora solo **el exceso sobre la
referencia**, que es la parte evitable.

**Ejemplo numérico** (libro sintético de 3 años, 2023): el margen EBITDA pasó de 22,8 % a
19,75 % con ingresos de 169.198.600.

- Pérdida = (0,228 − 0,1975) × 169.198.600 = **5.160.557,30**
- Margen afectado = 5.160.557,30 / 169.198.600 × 100 = **3,05 pp**
- Magnitud del deterioro = (0,228 − 0,1975) / 0,228 × 100 = **13,38 %**

Otro ejemplo, 2024: el costo de personal creció 9,66 % y los ingresos 7,41 %. La pérdida es
(9,66 % − 7,41 %) × costo de personal de 2023 = **641.172,88** (0,35 pp de margen).

#### Resumen y ranking

`GET /impact/summary` suma las pérdidas del año, las desglosa por categoría y lista los 5
problemas de mayor impacto. En la lista de problemas, el impacto ordena dentro de cada severidad.

> **Superposición:** los problemas de un mismo año pueden medir parte del mismo efecto (por
> ejemplo, el aumento del costo de personal también reduce el margen EBITDA). La pérdida total es
> la suma de las estimaciones y debe leerse como un orden de magnitud, no como una cifra contable.

| Año | Problema | Pérdida estimada | Margen afectado |
|---|---|---|---|
| 2023 | `GROSS_MARGIN_DECLINE` | 5.262.076,46 | 3,11 pp |
| 2023 | `EBITDA_DECLINE` | 5.160.557,30 | 3,05 pp |
| 2023 | `LABOR_COST_OUTPACES_REVENUE` | 56.809,11 | 0,03 pp |
| 2024 | `EBITDA_DECLINE` | 2.071.847,40 | 1,14 pp |
| 2024 | `GROSS_MARGIN_DECLINE` | 1.672.017,20 | 0,92 pp |
| 2024 | `LABOR_COST_OUTPACES_REVENUE` | 641.172,88 | 0,35 pp |
| 2024 | `OVERHEAD_OUTPACES_REVENUE` | 12.904,08 | 0,01 pp |

### Score Opsynex

`Opsynex.Domain/Engines/Scoring/ScoreEngine.cs` (clase pura) calcula un score de 0 a 100 por año.
Cada insumo es un KPI normalizado de 0 a 100 con los benchmarks de la industria y la moneda (ver
*Normalización*), y cada componente es el promedio de sus insumos disponibles.

| Componente | Peso | Insumos (0–100) |
|---|---|---|
| Rentabilidad | 30 % | Margen EBITDA, margen bruto |
| Eficiencia de costos | 25 % | Costo total / ingresos, gastos operacionales / ingresos |
| Productividad | 25 % | Ingresos por empleado (70 %) y su tendencia (30 %) |
| Estabilidad financiera | 20 % | Razón corriente, endeudamiento, volatilidad del margen EBITDA |

- **Tendencia de productividad:** variación anual de los ingresos por empleado. −10 % vale 0,
  0 % vale 50 y +10 % vale 100 (lineal, con límites). Si no hay año anterior, la productividad
  usa solo el nivel.
- **Volatilidad del margen:** desviación estándar del margen EBITDA en los últimos 3 años
  (incluido el actual), en pp. 0 pp vale 100 y 5 pp o más vale 0. Necesita al menos 2 años.
- **Flujo de caja operativo / ingresos:** no se usa porque el archivo del cliente no trae el
  estado de flujo de efectivo.

**Score total** = Σ (peso × componente) / Σ pesos de los componentes disponibles. Si a un
componente le faltan todos sus insumos, su peso se **redistribuye proporcionalmente** entre los
demás y la respuesta lo indica (`weightsRedistributed`, `missingReason`). Sin ningún componente
no se guarda score para ese año.

| Score | Interpretación |
|---|---|
| 80–100 | Operación saludable |
| 60–79 | Oportunidades de mejora |
| Menor a 60 | Deterioro operacional significativo |

Los pesos y parámetros se ajustan en la sección `Score` de `appsettings.json`. El API valida al
arrancar que los pesos sumen 1.

**Ejemplo numérico** (libro sintético de 3 años, 2024, todos los componentes disponibles):

| Componente | Insumos | Puntaje | Peso | Aporte |
|---|---|---|---|---|
| Rentabilidad | EBITDA 93,05 · bruto 60,17 | 76,61 | 0,30 | 22,98 |
| Eficiencia de costos | costo total 58,68 · gastos operacionales 86,48 | 72,58 | 0,25 | 18,15 |
| Productividad | ingresos por empleado 100 (sin tendencia) | 100 | 0,25 | 25,00 |
| Estabilidad financiera | razón corriente 100 · endeudamiento 92,09 · volatilidad 64,62 | 85,57 | 0,20 | 17,11 |
| **Total** | | | | **83,24 → Operación saludable** |

En 2022 y 2023 la nómina no trae empleados, así que falta la productividad y su 25 % se reparte:
rentabilidad 40 %, eficiencia de costos 33,33 % y estabilidad 26,67 %. Resultados: 2022 = 84,51 y
2023 = 80,20.

### Módulo industrial

> Pendiente (Etapa 11): OEE, scrap rate, downtime, cost per unit e impacto industrial.
