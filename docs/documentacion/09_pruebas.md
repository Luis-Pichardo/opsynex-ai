## Pruebas y calidad

Alcance previsto: pruebas de API, validación de datos y pruebas de KPIs, más pruebas E2E del
flujo completo.

### Base de datos en las pruebas (Etapa 01)

Las pruebas de integración (`Opsynex.IntegrationTests`) comparten una base de datos por
ejecución (`DatabaseFixture`):

- **Con Docker** (local, CI y nube con `setup_cloud.sh --with-sql`): levanta **SQL Server 2022
  real** con Testcontainers y aplica las migraciones. Es el modo por defecto.
- **Sin Docker:** usa SQLite in-memory con el esquema creado desde el modelo. Las pruebas que
  dependen de SQL Server (precisión decimal) se marcan como omitidas.
- `OPSYNEX_TEST_DB=sqlserver` exige Docker; `OPSYNEX_TEST_DB=sqlite` fuerza SQLite.

Cubren: crear y leer por empresa y año, filtro global por empresa, restricciones únicas
(empresa+año, empresa+cuenta, correo), auditoría de fechas, columnas JSON, borrado en cascada
y restricciones CHECK. Las pruebas unitarias verifican la configuración del modelo (índices
únicos, precisión, enums como texto, columnas JSON, filtros) y que no haya cambios del modelo
sin migración.

### Autenticación y permisos (Etapa 02)

- `Auth/AuthEndpointsTests`: login (claims del JWT, sin contraseña en la respuesta, mismo
  error para correo inexistente y contraseña errónea), `/me`, tokens ausentes, mal formados,
  con otra firma, vencidos o de un usuario cuyo rol cambió, y `register` por rol.
- `Auth/LockoutTests`: bloqueo al 5.º intento, `Retry-After`, desbloqueo por tiempo y por
  cambio de contraseña, expiración del token. Usa `TimeTravelApiFactory` con un
  `FakeTimeProvider` para adelantar el reloj sin esperas.
- `Auth/CompaniesEndpointsTests` y `Auth/UsersEndpointsTests`: CRUD, validaciones,
  conflictos y aislamiento por empresa (otra empresa → 404; selección de empresa del Admin).
- `Auth/PermissionMatrixTests`: cada política y endpoint real contra anónimo, Admin, Analyst y
  Client. Usa un controlador solo de pruebas (`TestPolicyController`) por política, porque los
  endpoints de carga y análisis llegan en etapas posteriores.

### Carga y validación de datos (Etapa 03)

Todas las pruebas usan los **libros sintéticos** de `backend/sample-data/` (nunca datos reales).
Para regenerarlos se necesita LibreOffice:

```bash
python3 backend/tests/TestData/generate_sample_workbooks.py
```

- `Parsing/WorkbookParserTests` (unitarias): clasificación de las 20 hojas, valores en bruto
  contra `expected_values.json` (ventas netas, descuentos, costo de ventas, total de activos,
  total de capital, energía de las notas…), nómina sin datos personales, serie mensual, balance
  de comprobación, cada inconsistencia de la sección 5 con su hoja y fila, archivos inválidos y
  plantillas.
- `Parsing/ParsingComponentTests` (unitarias): normalización de etiquetas, lectura de números
  contables, filtro de datos sensibles, clasificador, extractor, tolerancia e ISO 4217.
- `Uploads/UploadsEndpointsTests` y `DataEndpointsTests` (integración): carga del `.xls` y del
  `.xlsx`, valores guardados en la BD, privacidad (ningún nombre ni cédula en la BD ni en la
  respuesta), advertencias, 422 para archivos inválidos, 403 para el Client, 413 por tamaño,
  plantillas descargadas que el parser reconoce, y `/data` por tipo.

> Pendiente (Etapas 07, 10 y 12): estrategia completa, cobertura y checklist del MVP.
