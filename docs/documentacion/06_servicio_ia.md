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

> Pendiente (Etapa 11): anomalías, recomendaciones y resumen ejecutivo con Claude (prompts,
> modelo, costos y configuración).
