# Formato de Excel de entrada (estructura de los archivos del cliente)

Este documento describe la **estructura** del tipo de archivo que Opsynex AI debe aceptar y
analizar: el libro contable que preparan los contadores de las empresas clientes. Se elaboró a
partir de un archivo de muestra entregado por el cliente. **El archivo de muestra y sus datos no
se guardan en ninguna parte del proyecto**: aquí solo queda la estructura.

> Esta especificación manda sobre las suposiciones de formato que haya en las etapas. Si algo
> de este documento contradice un archivo de etapa, gana este documento.

> **Regla de datos:** ningún archivo, cifra, nombre ni dato real de un cliente puede aparecer
> en el código, en los tests, en los datos de ejemplo ni en la documentación. Las pruebas usan
> **archivos sintéticos** generados por un script, con la misma estructura y valores inventados
> (ver sección 6).

## 1. Características generales

| Característica | Cómo viene | Implicación para el sistema |
|---|---|---|
| Formato | **`.xls` (Excel 97-2003, BIFF8)**; también puede llegar como `.xlsx` | Leer `.xls` **y** `.xlsx` (y `.csv` para la plantilla simple). ClosedXML **no** lee `.xls`: usar **ExcelDataReader** |
| País y moneda | República Dominicana, **RD$ (DOP)** | Moneda por empresa (`Company.Currency = "DOP"`); formato `RD$` en la interfaz |
| Períodos | Estados **comparativos de 2 años** (año actual y anterior) en el mismo archivo | Un archivo genera datos para **varios años** |
| Estructura | **Libro contable de trabajo**, no una plantilla: unas 20 hojas con títulos, filas vacías, subtotales, notas y celdas combinadas | Se necesita un **parser inteligente** que detecte las hojas, la fila de encabezado (años) y las columnas de etiquetas y valores |
| Columnas | Varían por hoja (los años pueden estar en E/G, F/G, G/H, G/I, L/N…; las etiquetas en A, C o D) | No asumir posiciones fijas |
| Etiquetas | En español, con referencias como "(nota 3)" o "(9.1)", prefijos como "Menos:" o "más:" y espacios extra | Normalizar el texto antes de homologar |
| Fórmulas | Muchas celdas son fórmulas | Leer el **valor calculado** (cacheado), nunca la fórmula |
| Datos personales | La hoja de nómina trae nombres, cédulas y salarios; otras hojas traen RNC, nombres de proveedores y números de cuenta bancaria | **Nunca** guardarlos ni enviarlos a la IA: solo agregados (por ejemplo, la cantidad de empleados) |
| Encabezado de empresa | El nombre de la empresa aparece en la primera fila de cada hoja; puede variar entre hojas | Advertir si los nombres no coinciden; no guardar ese texto más allá de la validación |

## 2. Hojas y uso en Opsynex

Nombres tal como aparecen en el archivo de muestra (pueden variar entre clientes):

| Hoja | Contenido | Uso | Prioridad |
|---|---|---|---|
| `BALANCE` | Balance general al 31 de diciembre, 2 años. Secciones en col. A (ACTIVOS CORRIENTES, PROPIEDAD PLANTA & EQUIPOS, OTROS ACTIVOS, PASIVO CORRIENTE, CAPITAL), cuentas en col. C, un valor por año | **Fuente principal**: activos, pasivos, patrimonio, efectivo, CxC, inventarios, CxP y deuda | MVP |
| `g&P` | Estado de ganancias (resultados), 2 años. Cuentas en col. C y D | **Fuente principal**: ingresos, descuentos, costo de ventas, gastos, D&A, gastos financieros, ISR y utilidad neta | MVP |
| `EPC` | Estado de cambios en el patrimonio de los socios (no es flujo de efectivo) | Validación cruzada del patrimonio | Opcional |
| `nota1` | Notas: efectivo por banco, cuentas por cobrar, propiedad planta y equipo, otros activos | Desglose del balance (validación) | MVP |
| `nota2` | Notas: préstamos, cuentas por pagar, acumulaciones y retenciones, deuda a largo plazo, cálculo del ISR | Desglose de pasivos e impuesto (validación) | MVP |
| `nota3` | Notas: compensaciones al personal (sueldos, regalía pascual, bonificaciones, seguridad social…) y gastos generales y administrativos (honorarios, energía eléctrica, combustibles, reparaciones, mantenimientos, seguros, etc.) | **Clave** para `LaborCost`, `EnergyCost`, `MaintenanceCost` y `OverheadCost` | MVP |
| `nota4` | Nota de costo de producción: inventarios de materia prima, materias primas importadas y locales, labor directa (detallada) y gastos de fabricación (energía, mantenimiento, combustibles, depreciación de maquinaria) | **Clave** para costos de producción, energía, mantenimiento, labor y depreciación en el costo | MVP |
| `REGPASCUAL` | Registro de empleados por departamento con sueldo mensual por mes | **Cantidad de empleados** (total y por departamento). Solo se guarda el conteo y los totales agregados | MVP |
| `PRESTLAB` | Prestaciones laborales de los empleados desvinculados en el año | **Rotación de personal** (conteo de salidas) | Fase 2 |
| `CUADRO` | Ventas, ITBIS y compras **mensuales** del año actual | **Serie mensual de ingresos** para tendencias y anomalías (etapa 11) | MVP+ |
| `IMPORT` | Relación de importaciones del año (FOB, flete, seguro, CIF, gravamen, ITBIS) | Costos de importación y dependencia de importaciones | Fase 2 |
| `BCE COMPROBA` | Balance de comprobación con **códigos de cuenta jerárquicos** (1 activo, 2 pasivo, 3 capital, 4 ventas, 5 costos, 6 gastos) y columnas por naturaleza (activo, pasivo y capital, ingresos, costos y gastos) | Homologación por código de cuenta. Puede **no cuadrar** (ver sección 5) | Validación |
| `export`, `anticipos`, `desc`, `CXP`, `ADICIONES`, `ANEXOA1`, `ANEXOB1` | Papeles de trabajo fiscales (anticipos de ISR, notas de crédito, anexos de la declaración IR-2 de la DGII, adiciones de activos) | Ignorar en el MVP; informar como "hoja no reconocida" | — |
| `BONIF.` | Puede venir vacía | Ignorar | — |

### Cómo reconocer cada hoja (no depender solo del nombre)

El parser clasifica cada hoja por su **contenido** (las primeras ~15 filas), con palabras clave
normalizadas (sin acentos y en minúsculas):

| Tipo | Palabras clave |
|---|---|
| Balance general | "balance general", "balances generales", "estado de situacion" |
| Estado de resultados | "estado de ganancias", "estado de resultados", "ganancias y perdidas" |
| Cambios en el patrimonio | "patrimonio", "cambios en el capital" |
| Notas | "notas a los estados financieros" |
| Empleados | encabezados "codigo", "nombre", "cedula", "cargo", "sueldo" |
| Ventas mensuales | columna "mes", los meses "enero"…"diciembre" y "ingresos" o "ventas" |
| Balance de comprobación | "balance de comprobacion" y una columna "cuenta" con códigos numéricos |

## 3. Algoritmo de lectura de estados financieros

1. **Fila de años:** buscar en las primeras ~20 filas la primera fila con 1 a 4 celdas numéricas
   enteras entre 1990 y el año actual + 1. Esas columnas son las **columnas de valores**, una
   por año.
2. **Etiqueta de cada fila:** tomar las celdas de texto a la izquierda de la primera columna de
   valores. El texto de la col. A suele ser la **sección** (se guarda como contexto); la
   etiqueta efectiva es el texto más a la derecha (col. C o D).
3. **Normalizar la etiqueta:** quitar "(nota N)", "(N.N)" y "nota N"; quitar prefijos "Menos:"
   y "más:"; colapsar espacios; conservar también el texto original.
4. **Clasificar la fila:** *título* (sin valores), *detalle* (con valores) o *subtotal/total*
   (la etiqueta empieza con "Total" o no tiene etiqueta pero sí valores). Los subtotales se
   guardan aparte, para validar, y **no** se suman como cuentas.
5. **Valores:** celdas numéricas o texto numérico (`1,234.56`, `(1,234)`, `RD$`). Una celda
   vacía es "sin dato", no cero.
6. **Guardar** cada fila de detalle como `RawAccountValue` con: hoja, tipo de hoja, fila,
   sección, etiqueta original, etiqueta normalizada, año y valor.

## 4. Mapeo a categorías Opsynex (referencia para la homologación)

| Fuente (hoja → etiqueta) | Categoría Opsynex |
|---|---|
| g&P → "Ventas netas" | `GrossSales` |
| g&P → "Descuentos sobre ventas" | `SalesDiscounts` |
| Derivado: Ventas netas − Descuentos | `Revenue` |
| g&P → "Costos de Ventas" | `Cogs` |
| g&P → "Compensaciones al personal" | `OperatingExpenses` (y `LaborCost`) |
| g&P → "Gastos Administrativos" | `OperatingExpenses` (y `OverheadCost`) |
| g&P → "Depreciación & Amortización" | `Depreciation` (dentro de opex) |
| g&P → "Gastos Financieros" | `FinancialExpenses` (no es opex; no entra en el EBITDA) |
| g&P → "Ingresos netos antes impuesto s/renta" | `EarningsBeforeTax` |
| g&P → "Impuesto sobre la renta" | `IncomeTax` |
| g&P → "Ingresos Netos" | `NetIncome` |
| nota3 → "Energía Eléctrica" + nota4 → "Energía Eléctrica" | `EnergyCost` |
| nota3 → "Reparaciones", "Mantenimientos", "Reparaciones & Mantenimiento Vehículos" + nota4 → "Reparaciones & mantenimientos" | `MaintenanceCost` |
| nota3 → total de compensaciones (nota 11) + nota4 → "Labor directa" | `LaborCost` |
| nota4 → "Depreciación Maquinarias & Equipos" | `Depreciation` (incluida en el costo) |
| nota4 → "Materias primas importadas" + "Compras de materias primas locales" | `RawMaterials` |
| BALANCE → "Efectivo en Cajas & Bancos" | `Cash` |
| BALANCE → "Cuentas por Cobrar" | `AccountsReceivable` |
| BALANCE → "Inventario Productos Terminados" + "Inventario Materia Prima" | `Inventory` |
| BALANCE → "Total corriente" (sección de activos) | `CurrentAssets` |
| BALANCE → "Total Activos" | `TotalAssets` |
| BALANCE → "Cuentas por Pagar" | `AccountsPayable` |
| BALANCE → "Total corriente" (sección de pasivos) | `CurrentLiabilities` |
| BALANCE → "Préstamos por Pagar" + "Préstamos a largo plazo" | `FinancialDebt` |
| BALANCE → "Total capital" | `Equity` |
| Derivado: Total Pasivos & Capital − Total capital | `TotalLiabilities` |
| REGPASCUAL → filas con código de empleado | `EmployeeCount` (conteo) |

Términos dominicanos que el diccionario de homologación debe conocer: ITBIS (IVA), ISR,
Regalía Pascual, Infotep, TSS / Tesorería de la Seguridad Social, Seguro Familiar de Salud,
Riesgos laborales, Preaviso & Cesantía, Prestaciones laborales, Anticipos, RNC, NCF, DGII, IR-2,
IR-3, IR-17, "Compensaciones al personal", "Gastos de Fabricación", "Labor directa".

**EBITDA** (no viene en el archivo, se deriva):
`EBITDA = Utilidad antes de ISR + Gastos financieros + D&A en gastos + Depreciación incluida en el costo de producción`.

## 5. Problemas de calidad típicos (deben detectarse sin rechazar la carga)

En la muestra aparecieron estos tipos de inconsistencias. El sistema debe reportarlos como
**advertencias** con hoja, fila y columna:

| # | Tipo de inconsistencia | Validación esperada |
|---|---|---|
| 1 | Total de activos ≠ total de pasivos y capital por diferencias de redondeo | Tolerancia (≤ 1 unidad o 0,01 %): ok; si es mayor, advertencia |
| 2 | Una nota no suma lo mismo que la línea del estado que desglosa | Advertencia "la nota no cuadra con el estado" |
| 3 | El estado de cambios en el patrimonio no refleja la ganancia del período | Advertencia de patrimonio (no bloquea) |
| 4 | El balance de comprobación no cuadra ("prueba cero" ≠ 0), tiene saldos bancarios o de inventario negativos, o sus ventas difieren del estado de resultados | Marcarlo como **no confiable** y usar los estados financieros como fuente de verdad |
| 5 | El nombre de la empresa difiere entre hojas | Advertencia |
| 6 | Hojas vacías u hojas fiscales no reconocidas | Informar como "hojas ignoradas", sin error |

Errores **bloqueantes** solo si no se encuentra ni balance ni estado de resultados, o si el
archivo es ilegible.

## 6. Archivos de prueba (sintéticos)

- Un script del repositorio (`backend/tests/TestData/GenerateSampleWorkbook` o equivalente)
  **genera** libros con esta misma estructura (mismos nombres de hoja, títulos, secciones,
  etiquetas, columnas desplazadas, subtotales, fórmulas, notas y nómina) pero con **valores y
  nombres inventados** (empresa ficticia, empleados "EMPLEADO 001", cédulas "000-0000000-0").
- Debe generar al menos: un `.xls` válido de 2 años, su versión `.xlsx`, una variante de 3 años
  pensada para disparar reglas de diagnóstico, y variantes con cada inconsistencia de la
  sección 5.
- Los valores esperados de los tests (KPIs, EBITDA, totales) se calculan a partir de esos datos
  inventados y se documentan junto al generador.
