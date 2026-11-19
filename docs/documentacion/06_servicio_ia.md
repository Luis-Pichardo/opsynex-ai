## Servicio de inteligencia artificial

Servicio independiente en **Python (FastAPI)** que complementa al API principal. No accede a la
base de datos: recibe del API información agregada (cuentas contables, KPIs, problemas) y
devuelve sugerencias. Solo el API puede invocarlo, con una clave interna.

Funciones previstas:

| Función | Técnica | Etapa |
|---|---|---|
| Sugerencias de homologación contable | Diccionario de sinónimos + coincidencia difusa | 04 |
| Detección de anomalías en KPIs | Estadística robusta (z-score con MAD) | 11 |
| Recomendaciones inteligentes por problema | Modelo de lenguaje (Claude) con salida estructurada | 11 |
| Resumen ejecutivo en lenguaje natural | Modelo de lenguaje (Claude) | 11 |

Principios: privacidad (solo datos agregados, sin datos personales), degradación con elegancia
(sin IA la plataforma funciona con recomendaciones base) y control de costos.

### Arquitectura y seguridad interna

- FastAPI + Pydantic v2. Las funciones de análisis son puras (`app/engines/`) y los endpoints
  están en `app/routers/`.
- **Stateless:** no tiene base de datos. Cada petición trae todo lo que necesita.
- **Solo lo llama el API .NET:** todas las rutas excepto `GET /health` exigen la cabecera
  `X-Internal-Api-Key` (variable `AI_SERVICE_API_KEY`, igual a `AiService__ApiKey` del API).
  Sin la clave responde 401 y, si el servicio no tiene clave configurada, 503.
- **Privacidad:** el API solo envía etiquetas de cuentas (ya filtradas: sin números de cuenta,
  cédulas ni RNC), el tipo de hoja, la sección y el código de cuenta. Nunca montos de la nómina,
  nombres ni datos personales.
- **Resiliencia en el API:** cliente HTTP tipado con `Microsoft.Extensions.Http.Resilience`
  (timeout total configurable, 10 s por defecto; reintentos con espera exponencial; circuit
  breaker). Si falla, el API usa su fallback local y la plataforma sigue funcionando. El health
  check `ai-service` del API queda en `Degraded`, no en `Unhealthy`.

### `POST /homologation/suggest`

Request:

```json
{
  "accounts": [
    { "id": "0", "label": "Energía Eléctrica", "normalized_label": "Energía Eléctrica",
      "sheet_type": "Notes", "section": "13. GASTOS GENERALES Y ADMINISTRATIVOS", "account_code": null },
    { "id": "1", "label": "Total corriente", "normalized_label": "Total corriente",
      "sheet_type": "BalanceSheet", "section": "PASIVO CORRIENTE", "account_code": null }
  ],
  "known_mappings": [ { "normalized_label": "Partida especial", "category": "overhead_cost", "section": null } ]
}
```

Response (una sugerencia por cuenta, en el mismo orden):

```json
{
  "suggestions": [
    { "id": "0", "label": "Energía Eléctrica", "category": "energy_cost", "confidence": 0.97,
      "method": "exact", "alternatives": [ { "category": "maintenance_cost", "confidence": 0.5 } ] },
    { "id": "1", "label": "Total corriente", "category": "current_liabilities", "confidence": 0.97,
      "method": "rule", "alternatives": [] }
  ]
}
```

Las categorías van en `snake_case` (`gross_sales`, `total_liabilities_and_equity`…) y equivalen
al enum `OpsynexCategory` del API.

### Motor de homologación (`app/engines/homologation`)

1. **Normalización:** minúsculas, sin acentos, sin "(nota N)", "(N.N)", "Menos:" ni "más:", sin
   códigos numéricos, y "&" → "y".
2. **Categorías posibles según el contexto:**
   - Balance → categorías del balance.
   - Estado de resultados → categorías de resultados.
   - Notas que desglosan el balance (efectivo, cuentas por cobrar, propiedad y equipo,
     préstamos…) → categorías del balance.
   - Balance de comprobación → el primer dígito del código orienta la categoría (1 activo,
     2 pasivo, 3 capital, 4 ventas, 5 costos, 6 gastos).
3. **Orden de búsqueda:**
   - Mapeo confirmado de la empresa (confianza 1,0, `known`).
   - Reglas de contexto: "Total corriente" según la sección (0,97, `rule`).
   - Coincidencia exacta con un sinónimo (0,97, `exact`).
   - Palabra clave contenida en la etiqueta, con prioridad por categoría y por el término más
     largo (0,80–0,95, `keyword`).
   - Coincidencia difusa con rapidfuzz `WRatio` ≥ 82 (`fuzzy`).
   - Si nada coincide: `other` con confianza 0,2 (`none`), y la cuenta queda dudosa.
4. **Alternativas:** las 3 categorías más parecidas, distintas de la elegida.

El diccionario (`dictionary.py`) tiene sinónimos en español e inglés por categoría y los términos
dominicanos: ITBIS, ISR, Regalía Pascual, Infotep, TSS, Seguro Familiar de Salud, Riesgos
laborales, Preaviso & Cesantía, Prestaciones laborales, "Compensaciones al personal", "Gastos de
Fabricación" y "Labor directa". También tiene las etiquetas que deben quedar en `other` a
propósito (ganancia bruta, propiedad planta y equipo, depreciación acumulada…), para que se
reconozcan con confianza alta y no se marquen como dudosas. Una misma etiqueta puede depender de
la hoja: "Gastos Administrativos" es gasto operacional en el estado de resultados y gasto
general en las notas.

### IA generativa: recomendaciones y resumen ejecutivo (Etapa 11)

```
AnalysisPipeline (API .NET) ──guarda KPIs, problemas, score──► SQL Server
        │ después de guardar (Ai:Enabled)
        ▼
IAiEnrichmentScheduler ── Background: cola en memoria + BackgroundService (producción)
        │                 Inline: en la misma petición (pruebas)
        ▼
AiEnrichmentService ──► 1. POST /anomalies/detect      (sin LLM)
                    ──► 2. POST /executive-summary     ──► Claude
                    ──► 3. POST /recommendations/generate (High y Medium) ──► Claude
```

- **El API .NET orquesta y guarda:**
  - `Problem.AiRecommendation`: JSON con diagnóstico, causas raíz y acciones;
  - `ExecutiveSummaries`: párrafos, prioridades, modelo y fecha;
  - problemas `ANOMALY_*` de severidad Low.
- **El servicio de IA sigue sin estado:** solo transforma datos en texto.
- **El análisis no espera a Claude:** en modo Background termina enseguida y la UI muestra
  «Generando…» hasta que el resultado está listo.
- **Caché:** cada entrada se resume con un hash SHA-256. Si los datos no cambiaron (por ejemplo,
  al volver a analizar lo mismo), no se vuelve a llamar a Claude. «Regenerar» fuerza la llamada.
  Los decimales se normalizan antes del hash: el mismo monto calculado o leído de SQL Server
  tiene distinta escala.
- **Degradación:** sin `ANTHROPIC_API_KEY` el servicio responde 503 `llm_not_configured`. El API
  lo registra, no hace más llamadas y la UI muestra «La IA generativa no está configurada. Las
  recomendaciones base siguen disponibles». Si el servicio de IA está caído o Claude falla, el
  estado es `unavailable`. Nada de esto afecta a KPIs, problemas, score, impacto ni al PDF.

#### Cliente de Claude (`app/llm/client.py`)

- **SDK y modelo:** SDK oficial `anthropic` (2.x, asíncrono). El modelo es `ANTHROPIC_MODEL`
  (por defecto `claude-opus-5-5`) y el esfuerzo es `ANTHROPIC_EFFORT` (por defecto `medium`, el
  predeterminado de Opus 5.5, fijado explícito). El pensamiento es adaptativo, el comportamiento
  por defecto del modelo.
- **Salida estructurada:** `client.beta.messages.parse(output_format=ModeloPydantic)`; la
  respuesta llega validada contra el esquema.
- **`stop_reason`:** se revisa antes de leer el contenido. `refusal` → 422 `llm_refused` (con la
  categoría); `max_tokens` → 502 `llm_truncated`; sin salida válida → 502 `llm_invalid_output`.
- **Respaldo ante rechazos:** `fallbacks="default"` con la beta `server-side-fallback-2026-07-01`.
  Si el modelo declina por sus clasificadores de seguridad, el servidor reintenta con el modelo
  recomendado. `ANTHROPIC_FALLBACKS=none` lo desactiva (necesario en modelos sin esta función,
  como Haiku).
- **Errores por tipo:**

  | Error del SDK | Respuesta del servicio |
  |---|---|
  | `RateLimitError` | 429 `llm_rate_limited` |
  | `AuthenticationError` / `PermissionDeniedError` | 503 `llm_not_configured` |
  | `NotFoundError` | 503 `llm_bad_model` |
  | `BadRequestError` | 502 `llm_bad_request` |
  | `APITimeoutError` | 504 `llm_timeout` |
  | 5xx | 502 `llm_upstream_error` |
  | `APIConnectionError` | 503 `llm_unavailable` |

  Timeout por llamada: `LLM_TIMEOUT_SECONDS` (90 s). Reintentos del SDK: `LLM_MAX_RETRIES` (2).
  El API .NET no reintenta, para no duplicar el costo.
- **Prompt caching:** el system prompt es fijo y lleva `cache_control`; los datos variables van al
  final, en el mensaje del usuario, como JSON con claves ordenadas. Hoy el prompt fijo es más
  corto que el mínimo cacheable del modelo, así que la caché se activará sola si crece (por
  ejemplo, con un glosario más largo). Se verifica en los logs con `cache_read`.
- **Registro de uso:** cada llamada escribe `llm.call purpose=… model=… stop=… input=… output=…
  cache_write=… cache_read=…`.

#### Endpoints (protegidos con `X-Internal-Api-Key`)

| Endpoint | Entrada | Salida |
|---|---|---|
| `POST /recommendations/generate` | `company` (industria, sector, país, moneda, año) y `problem` (código, título, descripción, severidad, categoría, impacto, margen afectado, evidencia, recomendación base, KPIs relacionados) | `{ summary, root_causes[], actions[{ title, description, expected_impact, effort: low/medium/high, horizon: short/medium/long }], model }` |
| `POST /executive-summary` | `company`, `score` (total, interpretación, componentes), score anterior, KPIs con el año anterior, top de problemas, hallazgos positivos, impacto total e ingresos | `{ paragraphs (3 a 5), priorities (3), model }` |
| `POST /anomalies/detect` | `series[{ name, label, unit, points[{ period, value }] }]`, `currency`, `threshold` (3,5), `min_deviation` (0,25) | `{ anomalies[{ series, period, value, expected, score, direction, method, explanation }] }` |

#### Prompts (resumen)

- **System prompt fijo:**
  - Analista financiero-operacional senior que escribe para la gerencia, en español neutro con
    tuteo.
  - Contexto del país: República Dominicana y RD$, con la terminología local (ITBIS, ISR, TSS,
    Regalía Pascual, INFOTEP, DGII, NCF). La adapta si el país o la moneda son otros.
  - Reglas: usar solo las cifras dadas y no inventar datos; montos con el símbolo de la moneda y
    márgenes en pp; acciones concretas; no mencionar personas; aclarar que es orientativo.
- **Tareas:**
  - Recomendación: diagnóstico de 2 o 3 frases, 2 a 4 causas raíz y 3 a 5 acciones con esfuerzo
    y horizonte, mejorando la recomendación base.
  - Resumen: de 3 a 5 párrafos (salud general, problemas de mayor impacto, mejoras) y
    exactamente 3 prioridades.
- El país sale de la moneda: DOP → República Dominicana. Para otras monedas se usa `Ai:Country`.

#### Anomalías (`app/engines/anomalies.py`, sin LLM)

- **Con 6 o más puntos:** z-score robusto, `z = 0,6745 · (x − mediana) / MAD`. Es atípico si
  |z| > 3,5 **y** se aleja al menos un 25 % de la mediana (materialidad). En una serie estable la
  MAD es tan pequeña que un pico estacional moderado, como diciembre +22 % en el libro sintético,
  daría z ≈ 4,2 sin ser raro.
- **Con menos puntos o MAD = 0:** variaciones bruscas de más de ±50 % frente al período anterior.
- **Serie usada:** los ingresos mensuales (la hoja de ventas mensuales del libro, 12 puntos). Cada
  anomalía se convierte en el problema `ANOMALY_REVENUE_AAAA_MM`:
  - severidad Low, categoría de impacto `Anomaly`, sin pérdida cuantificada;
  - la explicación en español y una recomendación de verificar el registro del mes.
- Los problemas `ANOMALY_*` los administra el paso de IA: el motor de reglas no los borra, y se
  eliminan cuando la anomalía ya no se detecta.

#### Privacidad

- **El API .NET solo envía agregados:** KPIs, montos consolidados, títulos y descripciones de
  problemas, score y la serie de ventas mensuales. De la nómina solo se usa la cantidad de
  empleados. Nunca salen nombres, cédulas, salarios individuales ni archivos.
- **Defensa en profundidad:** el servicio revisa el texto antes de cada llamada a Claude y la
  cancela (422 `llm_personal_data_blocked`) si encuentra algo con forma de cédula, RNC, correo,
  teléfono o fila de nómina («EMPLEADO 001»).
- **Pruebas:**
  - `AiInsightsEndpointsTests.Payloads_ContainOnlyAggregates_NeverPersonalData` captura todos los
    payloads generados con el libro sintético, que incluye su nómina, y verifica que no contienen
    filas de empleados, cédulas, RNC ni la hoja de nómina.
  - `tests/test_privacy.py` y `tests/test_llm_client.py` verifican el bloqueo antes de llamar a
    Claude.

#### Costos (estimación)

Precios de Claude Opus 5.5: US$ 4 por millón de tokens de entrada y US$ 20 por millón de salida
(el razonamiento cuenta como salida).

| Llamada | Entrada | Salida (con razonamiento, esfuerzo `medium`) | Costo aproximado |
|---|---|---|---|
| Recomendación de un problema | ~1.500–2.500 tokens | ~1.500–4.000 tokens | US$ 0,04–0,09 |
| Resumen ejecutivo | ~2.000–3.500 tokens | ~1.500–4.000 tokens | US$ 0,04–0,10 |

**Por análisis:**
- Un resumen más las recomendaciones de los problemas High y Medium del año, con un tope de
  `Ai:MaxRecommendations` (10).
- El libro sintético (2024 sin problemas High ni Medium) cuesta unos US$ 0,05–0,10.
- Una empresa con 5 problemas relevantes, unos US$ 0,25–0,55.
- El peor caso con el tope, alrededor de US$ 1.
- Volver a analizar los mismos datos no cuesta nada, por la caché por hash.

Los tokens reales quedan en los logs (`llm.call …`). Para abaratar: `ANTHROPIC_EFFORT=low`,
bajar `Ai:MaxRecommendations` o usar un modelo más económico (`ANTHROPIC_MODEL`; con Haiku,
además `ANTHROPIC_FALLBACKS=none`).

#### Cómo desactivar la IA

| Qué | Cómo |
|---|---|
| Todo el paso de IA (anomalías, resumen y recomendaciones) | `Ai__Enabled=false` en el API: la UI muestra «La IA está desactivada» |
| Solo la IA generativa (Claude) | No configurar `ANTHROPIC_API_KEY` en el servicio de IA: las anomalías siguen funcionando |
| El servicio de IA completo (también la homologación con IA) | `AiService__Enabled=false`: la homologación usa el fallback local |

Configuración del API (sección `Ai`): `Enabled`, `Mode` (`Background`/`Inline`),
`RecommendationSeverities` (`High`, `Medium`), `MaxRecommendations` (10), `DetectAnomalies`,
`Country` y `TimeoutSeconds` (180).

#### Series diarias del módulo industrial (etapa 12)

Si la empresa tiene producción, el API envía además, por año, la serie diaria de la planta de OEE
(`DailyOee{año}`, unidad `Ratio`) y de minutos de parada (`DailyDowntime{año}`, unidad `Number`),
con períodos `AAAA-MM-DD`. Con decenas de puntos, el z-score robusto sí tiene base estadística.
Cada anomalía se convierte en el problema `ANOMALY_OEE_AAAA_MM_DD` u `ANOMALY_DOWNTIME_AAAA_MM_DD`
(severidad Baja, sin impacto cuantificado), con su recomendación de revisar la bitácora del día.
Las anomalías se detectan aunque la empresa no tenga estados financieros. Las explicaciones
empiezan con «El valor de {serie} en {período}…».
