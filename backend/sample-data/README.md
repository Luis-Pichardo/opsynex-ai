# Datos de prueba sintéticos

Generados por `backend/tests/TestData/generate_sample_workbooks.py` (no editar a mano):

```bash
python3 backend/tests/TestData/generate_sample_workbooks.py
```

Reproducen la **estructura** de los libros contables de los clientes
(`docs/especificaciones/FORMATO_EXCEL_ENTRADA.md`) con una empresa ficticia
(«INDUSTRIAS FICTICIAS DEL CARIBE, S.R.L.») y **valores inventados**. No contienen datos reales: los empleados son
"EMPLEADO 001…", las cédulas "000-0000000-0" y los RNC "000-00000-0". Las fórmulas tienen sus
valores calculados por LibreOffice, como en un archivo guardado por Excel.

## Archivos

| Archivo | Descripción |
|---|---|
| `libro_sintetico_2a.xls` | Libro válido de 2 años (caso principal). |
| `libro_sintetico_2a.xlsx` | Libro válido de 2 años (caso principal). |
| `libro_sintetico_3a.xls` | Libro de 3 años con márgenes que se deterioran (para el diagnóstico). |
| `libro_sintetico_3a.xlsx` | Libro de 3 años con márgenes que se deterioran (para el diagnóstico). |
| `inc1_balance_no_cuadra.xls` | Total pasivos & capital del año actual 50.000 por encima del total de activos (0,04 %, fuera de la tolerancia de 0,01 %). |
| `inc2_nota_no_cuadra.xls` | La nota 4 (cuentas por cobrar) suma 75.000 más que la línea del balance. |
| `inc3_patrimonio.xls` | El estado de cambios en el patrimonio no refleja la ganancia del año actual. |
| `inc4_comprobacion.xls` | El balance de comprobación no cuadra, tiene bancos negativos y sus ventas difieren del estado de resultados. |
| `inc5_nombre_empresa.xls` | La hoja nota3 trae el nombre de la empresa escrito distinto. |
| `inc_subtotal.xls` | El total de gastos de operaciones no coincide con la suma de sus líneas. |
| `inc_anio_faltante.xls` | El estado de resultados solo trae el año actual. |
| `inc_cuentas_desconocidas.xls` | El estado de resultados trae dos líneas que ningún diccionario reconoce (revisión recomendada). |
| `invalido_anios.xlsx` | Las filas de años traen valores fuera de rango (1849/1850). |
| `invalido_sin_estados.xlsx` | Solo hojas fiscales: falta el balance y el estado de resultados → error bloqueante. |
| `invalido_vacio.xlsx` | Libro sin datos → error bloqueante. |
| `invalido_extension_falsa.xls` | Texto con extensión `.xls` → firma inválida. |
| `invalido_corrupto.xlsx` | Firma ZIP pero contenido dañado → ilegible. |
| `invalido_corrupto.xls` | Firma OLE2 pero contenido dañado → ilegible. |
| `plantilla_estado_resultados.csv` | Plantilla simple CSV de estado de resultados (2 años). |
| `plantilla_operacional.xlsx` | Plantilla simple de datos operacionales (2 años). |

## Valores esperados del libro de 2 años (RD$)

| Concepto | 2023 | 2024 |
|---|---:|---:|
| Ventas netas (GrossSales) | 172,300,000.00 | 185,450,000.00 |
| Descuentos sobre ventas | 3,101,400.00 | 3,709,000.00 |
| Revenue (ventas − descuentos) | 169,198,600.00 | 181,741,000.00 |
| Costo de ventas | 120,175,600.00 | 130,755,200.00 |
| Compensaciones al personal | 12,177,866.67 | 13,446,800.00 |
| Gastos administrativos | 7,920,000.00 | 8,520,000.00 |
| Depreciación & amortización (gastos) | 2,200,000.00 | 2,350,000.00 |
| Depreciación en el costo | 4,500,000.00 | 4,800,000.00 |
| Gastos financieros | 3,480,000.00 | 3,120,000.00 |
| Ganancia antes de ISR | 23,245,133.33 | 23,549,000.00 |
| ISR (27 %) | 6,276,186.00 | 6,358,230.00 |
| Utilidad neta | 16,968,947.33 | 17,190,770.00 |
| EBITDA | 33,425,133.33 | 33,819,000.00 |
| Efectivo | 26,456,859.33 | 34,225,129.33 |
| Cuentas por cobrar | 16,327,500.00 | 18,340,000.00 |
| Inventarios | 14,650,000.00 | 16,180,000.00 |
| Total corriente (activos) | 57,974,359.33 | 69,355,129.33 |
| Total activos | 123,024,359.33 | 133,955,129.33 |
| Total corriente (pasivos) | 24,335,000.00 | 25,075,000.00 |
| Deuda financiera | 29,700,000.00 | 24,800,000.00 |
| Total pasivos | 46,335,000.00 | 43,075,000.00 |
| Total capital | 76,689,359.33 | 90,880,129.33 |
| Energía (nota 3 + nota 4) | 9,850,000.00 | 11,210,000.00 |

Nómina (`REGPASCUAL`, 2024): **15 empleados**
(Produccion: 8, Administracion: 4, Ventas: 3); nómina anual
total RD$ 7,598,000.00.

`EBITDA = Ganancia antes de ISR + Gastos financieros + D&A en gastos + Depreciación en el costo`.

## KPIs esperados del libro de 2 años

Calculados a mano con estos valores (ratios con 4 decimales; días con 2). Ingresos por empleado
solo en 2024, el año de la nómina; costo por unidad no aplica (el libro no trae unidades).

| KPI | 2023 | 2024 |
|---|---:|---:|
| EbitdaMargin | 0.1975 | 0.1861 |
| GrossMargin | 0.2897 | 0.2805 |
| CostEfficiency | 0.842 | 0.8533 |
| RevenuePerEmployee | — | 12116066.67 |
| OperatingCostRatio | 0.1318 | 0.1338 |
| OverheadRatio | 0.0468 | 0.0469 |
| EnergyCostRatio | 0.0582 | 0.0617 |
| CostPerUnit | — | — |
| NetMargin | 0.1003 | 0.0946 |
| LaborCostRatio | 0.1688 | 0.1723 |
| CurrentRatio | 2.3823 | 2.7659 |
| DebtToEquity | 0.6042 | 0.474 |
| DaysSalesOutstanding | 35.22 | 36.83 |
| InventoryDays | 44.5 | 45.17 |

Todos los valores, por archivo, año y concepto, están en `expected_values.json`, que leen las
pruebas automáticas.

## Módulo industrial (`industrial/`)

Generados por `backend/tests/TestData/generate_industrial_samples.py` (semilla fija, valores
inventados): una planta ficticia con 3 máquinas en 2 líneas y producción diaria de lunes a sábado
del 1 de octubre al 31 de diciembre de 2024.

```bash
python3 backend/tests/TestData/generate_industrial_samples.py
```

| Archivo | Descripción |
|---|---|
| `produccion.xlsx` | 234 registros diarios (fecha, máquina, línea, minutos planificados y de parada, unidades totales, buenas y producidas, ciclo real). |
| `costos.xlsx` | Costos mensuales por máquina (mano de obra, materiales, energía y gastos generales). |
| `estandares.xlsx` | Ciclo estándar, costo estándar por unidad y costo por minuto de cada máquina. |
| `produccion_con_errores.csv` | Encabezados de la especificación en inglés y filas inválidas (buenas > totales, sin máquina, fecha inválida, parada > planificado, repetida). |
| `expected_industrial.json` | KPIs e impacto esperados, calculados por el generador de forma independiente al motor .NET. |

| Máquina | Línea | Comportamiento | Problemas que genera |
|---|---|---|---|
| MAQ-01 | LINEA-1 | Sana (OEE ≈ 89 %). | Ninguno. |
| MAQ-02 | LINEA-1 | Scrap ≈ 8 % y costo por unidad ≈ 18 % sobre el estándar. | `HIGH_SCRAP`, `COST_PER_UNIT_ABOVE_STANDARD`. |
| MAQ-03 | LINEA-2 | Paradas ≈ 25 % del tiempo y ciclo lento (OEE ≈ 57 %). | `HIGH_DOWNTIME`, `LOW_OEE`. |

El 14/11/2024 hay una avería mayor en MAQ-02 y MAQ-03: es la anomalía de las series diarias de
OEE y minutos de parada.
