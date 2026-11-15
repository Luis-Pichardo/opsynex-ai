# Etapa 04 — Homologación contable + servicio de IA (sugerencias)

**Depende de:** 03 · **Rama:** `etapa-04-homologacion`

## Prompt

> Implementa el motor de homologación contable (sección 7 de las especificaciones): mapear las
> cuentas del cliente a las categorías estándar de Opsynex. Las **sugerencias** las genera el
> servicio de IA en Python (diccionario + coincidencia difusa); el API .NET guarda los mapeos,
> gestiona la confirmación del analista y consolida en `FinancialData`. Si el servicio de IA
> no responde, el API usa un fallback local por coincidencia exacta.

## Tareas

1. **Servicio de IA** (`ai-service`):
   - `app/engines/homologation/dictionary.py`: sinónimos en español e inglés por categoría
     ("ventas", "ingresos", "sales" → `revenue`; "costo de ventas", "cogs" → `cogs`;
     "energía", "electricidad" → `energy_cost`; "nómina indirecta", "gastos administrativos" →
     `overhead_cost`; "desperdicio", "merma", "scrap" → `material_loss`; "mantenimiento" →
     `maintenance_cost`; depreciación, intereses, impuestos, activos, pasivos, patrimonio,
     flujo operativo, etc.).
   - Motor puro: normaliza el texto (minúsculas, sin acentos, sin códigos contables numéricos)
     y busca coincidencia exacta → palabras clave → coincidencia difusa (`rapidfuzz`).
     Devuelve `{ category, confidence (0–1), method, alternatives[] }`.
   - `POST /homologation/suggest`: recibe `{ accounts: [...], known_mappings: [...] }` y
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
       categoría y año y hace upsert en `FinancialData`. Calcula derivados si faltan
       (`Ebitda = Revenue − Cogs − OperatingExpenses + Depreciation` si la depreciación está
       incluida en opex; `TotalCost = Cogs + OperatingExpenses`). Marca el upload como `Processed`.
     - Con `auto=true` consolida solo si todas las sugerencias tienen confianza ≥ 0.9.
3. Tests con los ejemplos del PDF: "Gasto Energía Planta" → `energy_cost`, "Nómina Indirecta" →
   `overhead_cost`, "Desperdicio Materia Prima" → `material_loss`, "Gasto Mantenimiento" →
   `maintenance_cost`. Tests de integración con el cliente de IA simulado (stub) y del fallback.

## Criterios de aceptación

- [ ] Los 4 ejemplos del PDF se mapean automáticamente de forma correcta.
- [ ] Tras consolidar `sample-data/`, `FinancialData` tiene 3 años con valores que coinciden
      con los totales del Excel (verificado en un test).
- [ ] Un mapeo confirmado se reutiliza en la siguiente carga de la misma empresa.
- [ ] Con el servicio de IA apagado, la homologación sigue funcionando (fallback).

## Documentación a actualizar

- `05_motores_analiticos.md`: "Homologación contable" (algoritmo, categorías, ejemplos y fallback).
- `06_servicio_ia.md`: arquitectura del servicio, seguridad interna y endpoint `/homologation/suggest`.
- `04_api.md`: endpoints `/mappings`.
- `10_guia_usuario.md`: cómo revisar y confirmar el mapeo.
- `13_bitacora.md`.
