## API REST

API REST en ASP.NET Core, documentada con OpenAPI. Estructura prevista (prefijo `/api/v1`):

| Grupo | Endpoints |
|---|---|
| Autenticación | `POST /login`, `POST /register` |
| Datos | `POST /uploads`, `GET /data` |
| KPIs | `GET /kpis` |
| Problemas | `GET /problems`, `GET /problems/{id}` |
| Score | `GET /score` |

> Pendiente (Etapas 02 a 07): especificación detallada de cada endpoint (roles, request,
> response y errores).
