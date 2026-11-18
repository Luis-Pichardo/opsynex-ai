# Etapa 03 — Carga y validación de datos

**Depende de:** 02 · **Rama:** `etapa-03-carga-de-datos`

## Prompt

> Implementa el módulo de carga de datos (secciones 6 y 8.2 de las especificaciones) en el API
> .NET. El sistema **debe aceptar archivos con la estructura real de los clientes**, descrita
> en `docs/especificaciones/FORMATO_EXCEL_ENTRADA.md`: un libro contable `.xls` de varias hojas,
> con estados financieros comparativos de 2 años, notas y nómina. No hay archivos reales en el
> repositorio: crea un generador de libros sintéticos con esa estructura. Lee ese
> documento completo antes de empezar: manda sobre cualquier suposición de este archivo.
> Implementa un parser inteligente que reconozca las hojas por su contenido, extraiga las
> cuentas con sus valores por año, valide cuadres y calidad, y guarde todo en bruto para la
> homologación (etapa 04). También acepta una plantilla simple `.xlsx`/`.csv` como alternativa.

## Tareas

1. **Lectura de archivos** (`Infrastructure/Parsing`):
   - **ExcelDataReader** (+ `ExcelDataReader.DataSet` y el registro de
     `System.Text.Encoding.CodePages` para `.xls` con code page 1252) para `.xls` y `.xlsx`, y
     CsvHelper para `.csv`. Se leen valores calculados, nunca fórmulas.
   - Validar la extensión y la firma real del archivo (magic bytes: OLE2 `D0 CF 11 E0` para
     `.xls`, ZIP `50 4B` para `.xlsx`); límite `Upload:MaxMb` (por defecto 20 MB) y un máximo de
     filas por hoja.
2. **Clasificador de hojas** (`SheetClassifier`): según la sección 2 de la especificación,
   identifica el tipo de cada hoja por su contenido (título y encabezados): `BalanceSheet`,
   `IncomeStatement`, `EquityStatement`, `Notes`, `EmployeeRoster`, `MonthlySales`,
   `TrialBalance` o `Unknown`. Las hojas vacías o desconocidas se reportan como ignoradas.
3. **Extractor de estados financieros** (`FinancialStatementExtractor`): implementa el
   algoritmo de la sección 3 (fila de años, columnas de valores, etiqueta y sección, limpieza de
   "(nota N)" y "Menos:"/"más:", clasificación título/detalle/subtotal). Funciona igual para
   el balance, el estado de resultados y las notas, sin posiciones fijas.
4. **Extractores especiales:**
   - `EmployeeRosterExtractor`: cuenta los empleados (filas con código de empleado) en total y
     por departamento. **Solo** guarda conteos y totales salariales agregados; descarta
     nombres, cédulas y sueldos individuales.
   - `MonthlySalesExtractor`: serie mensual de ingresos (hoja `CUADRO`) → nueva entidad
     `MonthlyFinancialData` (CompanyId, Year, Month, Revenue, Purchases) con su migración.
   - `TrialBalanceExtractor`: cuentas con código jerárquico, descripción y columnas por
     naturaleza. Valida la "prueba cero"; si no cuadra, lo marca como **no confiable**
     (advertencia) y no se usa como fuente principal.
5. **Modelo** (migración nueva):
   - `RawAccountValue`: agregar `SheetName`, `SheetType`, `RowNumber`, `Section`,
     `OriginalLabel`, `NormalizedLabel`, `AccountCode` (nullable), `RowKind`
     (Detail/Subtotal/Total).
   - `FinancialData`: agregar `GrossSales`, `SalesDiscounts`, `FinancialExpenses`,
     `EarningsBeforeTax`, `IncomeTax`, `RawMaterials`, `Cash`, `AccountsReceivable`,
     `Inventory`, `CurrentAssets`, `CurrentLiabilities`, `AccountsPayable`, `FinancialDebt`.
   - `OpsynexCategory`: agregar las categorías nuevas de la sección 4 de la especificación.
   - `OperationalData.EmployeeCount` se llena desde la nómina cuando existe.
   - `Company.Currency` acepta `DOP` (RD$). Se valida contra ISO 4217.
6. **Validaciones de calidad** (sección 5 de la especificación), como advertencias con hoja,
   fila y columna: cuadre activos = pasivos + capital (tolerancia ≤ 1 unidad o 0,01 %), notas vs.
   estados, subtotales vs. suma de sus detalles, patrimonio, balance de comprobación, nombre de
   empresa distinto entre hojas, años faltantes. Errores **bloqueantes** solo si no se encuentra
   ni balance ni estado de resultados, o si el archivo es ilegible.
7. **Endpoints** (`UploadsController`):
   - `POST /api/v1/uploads` (multipart: `file`, `companyId` opcional para Admin; `fileType`
     ahora es opcional porque el tipo se detecta). Respuesta: `{ uploadId, status, years[],
     sheets: [{ name, detectedType, rowsExtracted, ignored, reason }], errors[], warnings[],
     preview }`.
   - `GET /api/v1/uploads`, `GET /api/v1/uploads/{id}` (reporte completo).
   - `GET /api/v1/uploads/templates/{fileType}`: plantilla simple `.xlsx` (alternativa para
     clientes sin estados formales).
   - `DataController` → `GET /api/v1/data?year=&type=`.
8. **Datos de prueba sintéticos** (sección 6 de la especificación):
   - Generador (script o proyecto de test) que crea un libro `.xls` con la **misma estructura**
     que la de los clientes: los mismos nombres de hoja, títulos, secciones, etiquetas, columnas
     desplazadas, subtotales, fórmulas, notas, nómina (con "EMPLEADO 001" y cédulas
     "000-0000000-0"), ventas mensuales, balance de comprobación y hojas fiscales; con una
     **empresa ficticia y valores inventados**. Para producir `.xls` se puede generar `.xlsx` y
     convertirlo con `soffice --headless --convert-to xls`.
   - Variantes: `.xlsx`, 3 años (para el diagnóstico), cada inconsistencia de la sección 5 de
     la especificación, y archivos inválidos (sin balance ni resultados, años inválidos, vacío,
     extensión falsa, corrupto).
   - Los valores esperados se documentan junto al generador. Los archivos generados se
     guardan en `backend/sample-data/`.

## Criterios de aceptación

- [ ] El libro sintético `.xls` y su versión `.xlsx` se cargan con éxito y se detectan sus dos años.
- [ ] Las hojas `BALANCE`, `g&P`, `nota1`–`nota4`, `REGPASCUAL`, `CUADRO` y `BCE COMPROBA` se
      clasifican correctamente; las hojas fiscales y `BONIF.` aparecen como ignoradas.
- [ ] Los valores en bruto coinciden con los valores inventados del generador (test automático:
      ventas netas, descuentos, costo de ventas, total de activos, total de capital, etc.).
- [ ] Se detecta la cantidad de empleados y **no** se guarda ningún nombre ni cédula (test que
      lo verifica).
- [ ] Cada inconsistencia de la sección 5 se reporta como advertencia con hoja y fila.
- [ ] Un usuario `Client` recibe 403 al subir. Hay tests unitarios del clasificador y de los
      extractores, y tests de integración del endpoint.

## Documentación a actualizar

- `04_api.md`: `/uploads`, `/data` y plantillas.
- `05_motores_analiticos.md`: sección "Ingesta y validación" (clasificación de hojas,
  algoritmo de extracción y validaciones de calidad).
- `03_modelo_datos.md`: columnas y entidades nuevas.
- `10_guia_usuario.md`: qué archivos acepta el sistema (el libro contable completo o la
  plantilla simple) y cómo interpretar las advertencias.
- `13_bitacora.md`.
