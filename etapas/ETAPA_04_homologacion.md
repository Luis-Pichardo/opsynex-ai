# Etapa 04 — Motor de homologación contable

**Depende de:** 03 · **Rama:** `etapa-04-homologacion`

## Prompt

> Implementa el motor de homologación contable (sección 7 de las especificaciones): mapear las
> cuentas del cliente a las categorías estándar de Opsynex, con sugerencias automáticas,
> confirmación del analista y consolidación en `financial_data`.

## Tareas

1. **Diccionario de sinónimos** `app/engines/homologation/dictionary.py`: palabras clave en
   español e inglés por categoría (ej. "ventas", "ingresos", "revenue", "sales" → `revenue`;
   "costo de ventas", "cogs" → `cogs`; "energía", "electricidad" → `energy_cost`;
   "nómina indirecta", "gastos administrativos" → `overhead_cost`; "desperdicio", "merma",
   "scrap" → `material_loss`; "mantenimiento" → `maintenance_cost`; EBITDA, depreciación,
   intereses, impuestos, activos, pasivos, patrimonio, flujo operativo…).
2. **Motor de sugerencias** (función pura): normaliza texto (minúsculas, sin acentos, sin
   códigos contables), busca coincidencias exactas → por palabras clave → difusas
   (`rapidfuzz`), y devuelve `{category, confidence, method}`. Prioriza mapeos ya
   confirmados por la empresa (`account_mappings`).
3. Endpoints `/api/v1/mappings`:
   - `GET /pending?upload_id=` — cuentas sin mapear con sugerencia.
   - `GET /` y `PUT /` — ver y guardar/confirmar mapeos (masivo).
   - `POST /consolidate?upload_id=` — agrega valores por categoría y año y hace upsert en
     `financial_data`. Calcula derivados si faltan: `ebitda = revenue - cogs - operating_expenses
     (+ depreciación si viene incluida)`, `total_cost = cogs + operating_expenses`.
     Marca el upload como `processed`.
4. Si todas las cuentas tienen sugerencia con confianza ≥ 0.9 permite consolidación automática
   (parámetro `auto=true`).
5. Tests con los ejemplos del PDF: "Gasto Energía Planta" → `energy_cost`, "Nómina Indirecta"
   → `overhead_cost`, "Desperdicio Materia Prima" → `material_loss`, "Gasto Mantenimiento" →
   `maintenance_cost`, y con `sample_data/`.

## Criterios de aceptación

- [ ] Los 4 ejemplos del PDF se mapean correctamente de forma automática.
- [ ] Tras consolidar `sample_data/`, `financial_data` tiene 3 años con valores correctos
      (verificados en test contra los totales del Excel).
- [ ] Un mapeo confirmado se reutiliza en la siguiente carga de la misma empresa.

## Documentación a actualizar

- `05_motores_analiticos.md`: sección "Homologación contable" (algoritmo, categorías, ejemplos).
- `04_api.md`: endpoints `/mappings`.
- `09_guia_usuario.md`: cómo revisar y confirmar el mapeo.
- `12_bitacora.md`.
