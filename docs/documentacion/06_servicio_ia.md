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

> Pendiente (Etapas 04 y 11): endpoints, prompts, modelo, costos y configuración.
