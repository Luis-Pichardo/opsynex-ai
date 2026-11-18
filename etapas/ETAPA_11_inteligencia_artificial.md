# Etapa 11 — Inteligencia artificial: recomendaciones, anomalías y resumen ejecutivo

**Depende de:** 10 · **Rama:** `etapa-11-inteligencia-artificial`

## Prompt

> Amplía el servicio de IA en Python para que Opsynex genere recomendaciones inteligentes y un
> resumen ejecutivo en lenguaje natural con Claude, y detecte anomalías estadísticas en los
> KPIs. El API .NET orquesta las llamadas y guarda los resultados; el frontend los muestra.
> Todo debe degradar con elegancia: sin IA, la plataforma funciona con las recomendaciones base.
> **Antes de escribir código que llame a Claude, usa la skill `claude-api`.**

## Tareas

1. **Cliente de Claude** (`ai-service/app/llm/`):
   - SDK oficial `anthropic`. Modelo configurable con `ANTHROPIC_MODEL` (por defecto
     `claude-opus-5-5`; el usuario puede elegir uno más económico) y `ANTHROPIC_API_KEY`.
   - Salida estructurada (JSON validado con Pydantic) para que la respuesta siempre tenga el
     formato esperado. Revisa el `stop_reason` (incluido `refusal`) antes de leer el contenido.
   - Manejo de errores por tipo (rate limit, errores 5xx, conexión), timeouts y logs del uso de tokens.
   - Prompt caching del system prompt (instrucciones fijas primero, datos variables al final).
   - **Privacidad**: solo se envían agregados y KPIs, nunca datos personales ni archivos crudos.
     Los archivos de los clientes traen nóminas con nombres, cédulas y salarios (ver
     `docs/especificaciones/FORMATO_EXCEL_ENTRADA.md`): un test debe verificar que ningún
     payload hacia el servicio de IA o hacia Claude contiene esos datos.
   - **Contexto del país:** el prompt indica la moneda (RD$) y el país de la empresa, para que
     las recomendaciones usen la terminología local (ITBIS, TSS, Regalía Pascual, etc.).
2. **Endpoints del servicio de IA**:
   - `POST /recommendations/generate`: recibe un problema (código, KPIs relacionados,
     evidencia, industria e impacto) y devuelve `{ summary, root_causes[], actions[{ title,
     description, expected_impact, effort: low|medium|high, horizon: short|medium|long }] }`
     en español.
   - `POST /executive-summary`: recibe score, KPIs, tendencias y top de problemas, y devuelve
     un resumen ejecutivo de 3 a 5 párrafos más 3 prioridades.
   - `POST /anomalies/detect`: detecta valores atípicos en series de KPIs (z-score robusto con
     MAD; con pocas observaciones, reglas de variación brusca). Devuelve las anomalías con su
     explicación. No usa LLM. Con la estructura de los clientes, la serie útil es la de
     **ingresos mensuales** (hoja de ventas mensuales, 12 puntos); pruébalo con series
     sintéticas que tengan meses atípicos.
   - Tests con el cliente de Claude simulado (sin llamadas reales en CI).
3. **API .NET**:
   - Paso opcional en `AnalysisPipeline` (configurable con `Ai:Enabled`): pedir las
     recomendaciones de IA de los problemas High y Medium y guardarlas en
     `Problem.AiRecommendation`. Pedir el resumen ejecutivo y guardarlo en una nueva entidad
     `ExecutiveSummary` (CompanyId, Year, Content, GeneratedAt, Model). Ejecutar la detección
     de anomalías y convertirlas en problemas `ANOMALY_*` de severidad Low.
   - Caché: no regenerar si los datos de entrada no cambiaron (hash de la entrada).
   - `POST /api/v1/ai/regenerate?year=` (Admin y Analyst) y `GET /api/v1/ai/summary?year=`.
4. **Frontend** (según la guía de diseño):
   - Tarjeta "Resumen ejecutivo" en el dashboard, con la etiqueta "Generado con IA", la fecha y
     el botón "Regenerar".
   - En el detalle del problema: pestañas "Recomendación base" y "Recomendación IA" (causas
     raíz y acciones con esfuerzo y horizonte).
   - Estados de carga y de error claros si la IA no está disponible.
5. **Costos**: registra en logs los tokens por llamada y documenta una estimación del costo
   por análisis.

## Criterios de aceptación

- [ ] Con `ANTHROPIC_API_KEY` configurada, el análisis de `sample-data/` genera recomendaciones
      y un resumen ejecutivo coherentes, en español.
- [ ] Sin clave o con el servicio de IA apagado, todo el MVP sigue funcionando y la UI lo indica.
- [ ] Los tests no hacen llamadas reales a la API de Claude.
- [ ] La detección de anomalías tiene tests con series sintéticas.

## Documentación a actualizar

- `06_servicio_ia.md`: arquitectura, endpoints, prompts (resumen), modelo, privacidad, costos y cómo desactivar la IA.
- `10_guia_usuario.md`: cómo interpretar las recomendaciones de IA (y sus límites).
- `04_api.md`: `/ai/*`.
- `13_bitacora.md`.
