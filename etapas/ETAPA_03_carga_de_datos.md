# Etapa 03 — Carga y validación de datos

**Depende de:** 02 · **Rama:** `etapa-03-carga-de-datos`

## Prompt

> Implementa el módulo de carga de datos (secciones 6 y 8.2 de las especificaciones) en el API
> .NET: subida de Excel/CSV de estados financieros y datos operacionales; validación de
> estructura, valores faltantes y formatos numéricos; normalización; plantillas descargables y
> archivos de ejemplo. La homologación de cuentas es la etapa 04: aquí el archivo se valida y
> las cuentas se guardan en bruto.

## Tareas

1. **Formato de entrada** (documentarlo y generar plantillas con ClosedXML):
   - Estados financieros: "cuenta por filas, años por columnas", `Cuenta | 2022 | 2023 | 2024`.
     Un archivo por tipo, o un Excel con una hoja por tipo (`EstadoResultados`,
     `BalanceGeneral`, `FlujoEfectivo`, `EstructuraCostos`).
   - Datos operacionales (tabular): `Año | UnidadesProducidas | Empleados |
     CostosOperacionales | ConsumoEnergiaKwh | CostoEnergia | ScrapEstimado`.
2. `UploadsController`:
   - `POST /api/v1/uploads` (multipart: `file`, `fileType`, `companyId` opcional para Admin).
     Acepta `.xlsx` y `.csv`; límite `Upload:MaxMb`; valida la extensión y la firma real del
     archivo (magic bytes); ignora fórmulas y macros (lee solo valores).
   - Parser en `Infrastructure/Parsing` (ClosedXML y CsvHelper) detrás de una interfaz
     `IFileParser`. Valida columnas requeridas, años válidos (1990 hasta el año actual + 1),
     celdas numéricas (acepta `1.234,56`, `1,234.56`, `(1234)` como negativo, `$` y `%`),
     filas vacías y cuentas duplicadas.
   - Respuesta: `{ uploadId, status, errors: [{ row, column, message }], warnings, preview }`.
   - Guarda el `Upload` y las filas en `RawAccountValue`. Los datos operacionales (ya
     estandarizados) van directo a `OperationalData` (upsert por año).
   - `GET /api/v1/uploads`: historial; `GET /api/v1/uploads/{id}`: detalle y reporte.
   - `GET /api/v1/uploads/templates/{fileType}`: descarga la plantilla.
3. `DataController` → `GET /api/v1/data?year=&type=`: datos financieros y operacionales
   consolidados de la empresa.
4. `backend/sample-data/`: archivos de una empresa manufacturera ficticia con 3 años
   (2022–2024), diseñados para que después se disparen varias reglas (EBITDA en caída,
   overhead que crece más rápido que los ingresos, revenue per employee en baja). Incluye
   también archivos con errores intencionales para los tests.

## Criterios de aceptación

- [ ] Los archivos válidos de `sample-data/` se cargan sin errores.
- [ ] Los archivos con errores (columna faltante, texto en un número, año inválido, archivo
      vacío, extensión falsa) devuelven reportes claros con fila y columna.
- [ ] Un usuario `Client` recibe 403 al subir.
- [ ] Hay tests unitarios del parser y tests de integración del endpoint.

## Documentación a actualizar

- `04_api.md`: `/uploads`, `/data` y plantillas.
- `05_motores_analiticos.md`: sección "Ingesta y validación".
- `10_guia_usuario.md`: cómo preparar y subir archivos.
- `13_bitacora.md`.
