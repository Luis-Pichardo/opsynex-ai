# Etapa 03 — Carga y validación de datos

**Depende de:** 02 · **Rama:** `etapa-03-carga-de-datos`

## Prompt

> Implementa el módulo de carga de datos (sección 6 y 8.2 de las especificaciones): subida de
> archivos Excel/CSV de estados financieros y datos operacionales, validación de estructura,
> valores faltantes y formatos numéricos, y normalización. Crea plantillas descargables y
> archivos de ejemplo. La homologación de cuentas se hace en la Etapa 04: aquí el archivo se
> guarda y valida, y las cuentas se extraen en bruto.

## Tareas

1. **Formato de entrada** (documentarlo y generar plantillas):
   - Estados financieros en formato "cuenta por filas, años por columnas":
     `account | 2022 | 2023 | 2024`. Un archivo por tipo (`income_statement`, `balance_sheet`,
     `cash_flow`, `cost_structure`) o un Excel con una hoja por tipo.
   - Datos operacionales en formato tabular: `year | units_produced | employee_count |
     operational_costs | energy_consumption_kwh | energy_cost | scrap_estimate`.
2. `POST /api/v1/upload` (multipart: file, file_type, company_id opcional para admin):
   - Acepta `.xlsx`, `.xls`, `.csv`; límite `MAX_UPLOAD_MB`; valida extensión y tipo MIME real.
   - Parsea con pandas; valida columnas requeridas, años válidos, celdas numéricas (acepta
     `1.234,56`, `1,234.56`, `(1234)` como negativo, `$`, `%`), filas vacías y duplicados.
   - Devuelve un reporte: `{status, errors: [{row, column, message}], warnings, preview}`.
   - Guarda registro en `uploads` y las filas extraídas en una tabla `raw_account_values`
     (upload_id, company_id, client_account, year, value) — crea la migración.
   - Los datos operacionales (ya estandarizados) se guardan directo en `operational_data`.
3. `GET /api/v1/data` — lista cargas y datos financieros/operacionales de la empresa (filtros por año y tipo).
4. `GET /api/v1/upload/templates/{file_type}` — descarga plantilla Excel.
5. `backend/sample_data/`: archivos de ejemplo de una empresa manufacturera ficticia con 3 años
   (2022–2024), diseñados para que luego se disparen varias reglas de diagnóstico (EBITDA en
   caída, overhead creciendo más rápido que ingresos, revenue per employee bajando).
6. Servicio `app/services/parsing/` separado del router, con tests unitarios por cada validación.

## Criterios de aceptación

- [ ] Los archivos de `sample_data/` se cargan sin errores.
- [ ] Archivos con errores (columna faltante, texto en número, año inválido, archivo vacío,
      extensión falsa) devuelven reportes claros con fila y columna.
- [ ] Un usuario `client` recibe 403 al subir.
- [ ] Tests pasan.

## Documentación a actualizar

- `04_api.md`: `/upload`, `/data`, plantillas.
- `05_motores_analiticos.md`: sección "Ingesta y validación" (reglas de validación).
- `09_guia_usuario.md`: cómo preparar y subir archivos (formato de plantillas).
- `12_bitacora.md`.
