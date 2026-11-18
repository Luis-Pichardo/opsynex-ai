# Etapa 04 — Homologación contable + servicio de IA (sugerencias)

**Depende de:** 03 · **Rama:** `etapa-04-homologacion`

## Prompt

> Implementa el motor de homologación contable (sección 7 de las especificaciones): mapear las
> cuentas del cliente a las categorías estándar de Opsynex. Las **sugerencias** las genera el
> servicio de IA en Python (diccionario + coincidencia difusa); el API .NET guarda los mapeos,
> gestiona la confirmación del analista y consolida en `FinancialData`. Si el servicio de IA
> no responde, el API usa un fallback local por coincidencia exacta.
> El caso que debe resolverse es la estructura real de los clientes: lee las secciones 4 y 5
> de `docs/especificaciones/FORMATO_EXCEL_ENTRADA.md` (tabla de mapeo y términos dominicanos).

## Tareas

1. **Servicio de IA** (`ai-service`):
   - `app/engines/homologation/dictionary.py`: sinónimos en español e inglés por categoría
     ("ventas", "ingresos", "sales" → `revenue`; "costo de ventas", "cogs" → `cogs`;
     "energía", "electricidad" → `energy_cost`; "nómina indirecta", "gastos administrativos" →
     `overhead_cost`; "desperdicio", "merma", "scrap" → `material_loss`; "mantenimiento" →
     `maintenance_cost`; depreciación, intereses, impuestos, activos, pasivos, patrimonio,
     flujo operativo, etc.). Incluye **todos los términos dominicanos** de la sección 4 de la
     especificación (ITBIS, ISR, Regalía Pascual, Infotep, TSS, Seguro Familiar de Salud,
     Preaviso & Cesantía, "Compensaciones al personal", "Gastos de Fabricación", "Labor
     directa", etc.) y las categorías nuevas de la etapa 03 (`GrossSales`, `SalesDiscounts`,
     `FinancialExpenses`, `EarningsBeforeTax`, `IncomeTax`, `RawMaterials`, `Cash`,
     `AccountsReceivable`, `Inventory`, `CurrentAssets`, `CurrentLiabilities`,
     `AccountsPayable`, `FinancialDebt`).
   - **Contexto en la sugerencia:** cada cuenta llega con `sheet_type`, `section` y
     `account_code`. Ejemplos: "Total corriente" en la sección de activos → `CurrentAssets`, y
     en pasivos → `CurrentLiabilities`. "Energía Eléctrica" en notas de gastos o de costo de
     producción → `EnergyCost`. En el balance de comprobación, el primer dígito del código
     orienta la categoría (1 activo, 2 pasivo, 3 capital, 4 ventas, 5 costos, 6 gastos).
   - Motor puro: normaliza el texto (minúsculas, sin acentos, sin códigos contables numéricos)
     y busca coincidencia exacta → palabras clave → coincidencia difusa (`rapidfuzz`).
     Devuelve `{ category, confidence (0–1), method, alternatives[] }`.
   - `POST /homologation/suggest`: recibe `{ accounts: [{ label, normalized_label, sheet_type,
     section, account_code }], known_mappings: [...] }` (nunca datos personales) y
     devuelve una sugerencia por cuenta. Los mapeos ya confirmados por la empresa tienen
     prioridad (confianza 1.0).
   - Tests con pytest.
2. **API .NET**:
   - `IAiServiceClient` en Application con su implementación tipada en Infrastructure
     (`HttpClient` + `Microsoft.Extensions.Http.Resilience`: timeout de 10 s, reintentos y
     circuit breaker), con la cabecera `X-Internal-Api-Key`. Health check del servicio de IA.
   - Fallback local si el servicio falla: coincidencia exacta con mapeos confirmados y un
     diccionario mínimo; esas sugerencias se marcan `method = "fallback"`.
   - `MappingsController`:
     - `GET /api/v1/mappings/pending?uploadId=`: cuentas sin mapear con su sugerencia.
     - `GET /api/v1/mappings` y `PUT /api/v1/mappings`: consultar y guardar o confirmar mapeos (masivo).
     - `POST /api/v1/mappings/consolidate?uploadId=&auto=false`: agrega los valores por
       categoría y año y hace upsert en `FinancialData` (**todos los años del archivo**).
       Marca el upload como `Processed`.
   - **Sin doble conteo:** cada mapeo tiene un `Role`: `Primary` (líneas de los estados
     financieros, que alimentan los totales: Revenue, Cogs, OperatingExpenses, activos…) o
     `Breakdown` (detalles de las notas, que solo alimentan categorías analíticas:
     EnergyCost, MaintenanceCost, LaborCost, RawMaterials). Las filas de subtotal y total no
     se suman; se usan para validar. Migración para la columna `Role` en `AccountMapping`.
   - **Derivados** (cuando no vienen en el archivo):
     `Revenue = GrossSales − SalesDiscounts`;
     `TotalCost = Cogs + OperatingExpenses`;
     `Ebitda = EarningsBeforeTax + FinancialExpenses + Depreciation` (la depreciación en gastos
     más la incluida en el costo de producción);
     `TotalLiabilities = (Total pasivos y capital) − Equity`.
     - Con `auto=true` consolida solo si todas las sugerencias tienen confianza ≥ 0.9.
3. Tests con los ejemplos del PDF: "Gasto Energía Planta" → `energy_cost`, "Nómina Indirecta" →
   `overhead_cost`, "Desperdicio Materia Prima" → `material_loss`, "Gasto Mantenimiento" →
   `maintenance_cost`. Tests de integración con el cliente de IA simulado (stub) y del fallback.
4. **Test de punta a punta con el libro sintético:** subirlo → sugerencias → consolidación
   automática → `FinancialData` de sus dos años igual a los valores esperados del generador (Revenue, Cogs, OperatingExpenses, TotalCost,
   FinancialExpenses, Ebitda, NetIncome, EnergyCost, MaintenanceCost, LaborCost, TotalAssets,
   TotalLiabilities y Equity).

## Criterios de aceptación

- [ ] Los 4 ejemplos del PDF se mapean automáticamente de forma correcta.
- [ ] Tras consolidar el libro sintético, `FinancialData` tiene sus dos años con los valores
      esperados del generador (verificado en un test).
- [ ] Al menos el 90 % de las líneas de detalle del balance, del estado de resultados y de las
      notas 3 y 4 (con las etiquetas de la sección 4 de la especificación) reciben una sugerencia correcta con confianza ≥ 0,8.
- [ ] No hay doble conteo entre los estados y sus notas (test).
- [ ] Un mapeo confirmado se reutiliza en la siguiente carga de la misma empresa.
- [ ] Con el servicio de IA apagado, la homologación sigue funcionando (fallback).

## Documentación a actualizar

- `05_motores_analiticos.md`: "Homologación contable" (algoritmo, categorías, ejemplos y fallback).
- `06_servicio_ia.md`: arquitectura del servicio, seguridad interna y endpoint `/homologation/suggest`.
- `04_api.md`: endpoints `/mappings`.
- `10_guia_usuario.md`: cómo revisar y confirmar el mapeo.
- `13_bitacora.md`.
