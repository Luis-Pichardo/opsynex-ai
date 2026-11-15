# Parte II: Documentación técnica

## Arquitectura del sistema

### Stack tecnológico

| Capa | Tecnología |
|---|---|
| API principal | C# / ASP.NET Core (.NET 10), Entity Framework Core |
| Base de datos | SQL Server 2022 |
| Servicio de IA | Python 3.12 + FastAPI, Claude (Anthropic), scikit-learn, rapidfuzz |
| Frontend | React 18 + TypeScript + Vite + Tailwind CSS + shadcn/ui + Recharts |
| Hosting | Azure, AWS o GCP |

#### Versiones exactas (Etapa 00)

| Componente | Paquete / herramienta | Versión |
|---|---|---|
| API | .NET SDK / ASP.NET Core runtime | 10.0.115 / 10.0.15 |
| API | Microsoft.AspNetCore.OpenApi | 10.0.15 |
| API | Scalar.AspNetCore | 2.21.0 |
| API | Serilog.AspNetCore | 10.0.0 |
| API | Asp.Versioning.Mvc / ApiExplorer / OpenApi | 10.2.1 / 10.2.1 / 10.2.3 |
| Aplicación | FluentValidation | 12.2.0 |
| Pruebas .NET | xunit.v3 · Shouldly · Microsoft.AspNetCore.Mvc.Testing | 3.2.2 · 4.3.0 · 10.0.15 |
| Base de datos | SQL Server (imagen `mcr.microsoft.com/mssql/server:2022-latest`) | 2022 |
| IA | Python | 3.12 |
| IA | fastapi · uvicorn · pydantic · pydantic-settings | 0.163.1 · 0.54.0 · 2.14.3 · 2.16.3 |
| IA | pandas · scikit-learn · rapidfuzz · anthropic | 3.1.1 · 1.9.1 · 3.14.6 · 2.8.0 |
| IA (dev) | pytest · httpx · ruff | 9.2.0 · 0.28.1 · 0.17.5 |
| Frontend | Node.js (mínimo) | 20.19 (CI usa 22) |
| Frontend | react · react-dom | 18.3.1 |
| Frontend | vite · @vitejs/plugin-react · typescript | 8.3.8 · 6.3.0 · 5.9 |
| Frontend | tailwindcss · shadcn/ui (estilo new-york) | 3.4.19 · componentes copiados |
| Frontend | @tanstack/react-query · lucide-react | 5.107.1 · 1.73.0 |
| Frontend (dev) | vitest · @testing-library/react · eslint · prettier | 5.1.0 · 16.3.3 · 10.15.0 · 3.10.1 |

Las versiones de .NET se fijan en `backend/Directory.Packages.props`; las de Python, como
mínimos en `ai-service/pyproject.toml`; y las del frontend, en `frontend/package-lock.json`.

### Componentes

| Componente | Se comunica con | Cómo |
|---|---|---|
| Frontend (React) | API ASP.NET Core | HTTPS + token JWT |
| API ASP.NET Core | SQL Server | Entity Framework Core |
| API ASP.NET Core | Servicio de IA (Python) | HTTP interno con clave de servicio |
| Servicio de IA | Claude (Anthropic) | API de Anthropic (solo datos agregados) |

El API .NET concentra los datos, la seguridad y la lógica determinista (KPIs, reglas, impacto
y score). El servicio de IA no tiene base de datos: recibe información agregada y devuelve
sugerencias. Si el servicio de IA no está disponible, la plataforma sigue funcionando.

### Diagrama de componentes

```
┌──────────────────────┐   HTTPS + JWT    ┌──────────────────────────┐   EF Core   ┌─────────────────┐
│  Frontend (React)    │ ───────────────► │  API ASP.NET Core (.NET) │ ──────────► │ SQL Server 2022 │
│  Vite · shadcn/ui    │                  │  /api/v1 · /health       │             └─────────────────┘
│  :5173 (dev) / :8081 │                  │  :5080 (dev) / :8080     │
└──────────────────────┘                  └────────────┬─────────────┘
                                                       │ HTTP interno
                                                       │ X-Internal-Api-Key
                                                       ▼
                                          ┌──────────────────────────┐   HTTPS   ┌──────────────┐
                                          │ Servicio de IA (FastAPI) │ ────────► │ Claude API   │
                                          │ stateless · :8001        │           │ (Anthropic)  │
                                          └──────────────────────────┘           └──────────────┘
```

- El frontend **solo** llama al API .NET.
- El servicio de IA no tiene base de datos y exige la cabecera `X-Internal-Api-Key` en todas
  las rutas excepto `/health`.
- Si el servicio de IA no responde, el API sigue funcionando (fallbacks y timeouts, Etapa 04).

### Capas del API (arquitectura limpia)

| Proyecto | Responsabilidad | Depende de |
|---|---|---|
| `Opsynex.Domain` | Entidades, enums y motores puros (KPIs, reglas, impacto, score) | nada |
| `Opsynex.Application` | Servicios, DTOs, validadores (FluentValidation) e interfaces | Domain |
| `Opsynex.Infrastructure` | EF Core, repositorios, parsers Excel/CSV, cliente del servicio de IA | Application |
| `Opsynex.Api` | Controllers, middleware, autenticación, `Program.cs` | Application, Infrastructure |

Unas pruebas de arquitectura (`LayerDependencyTests`) verifican que estas dependencias se respeten.

Convenciones transversales del API ya configuradas:

- **Rutas versionadas** `/api/v1/...` (Asp.Versioning, versión en el segmento de URL).
- **Errores** en formato ProblemDetails (RFC 7807) con `code` propio y `traceId`
  (por ejemplo, `404` → `"code": "not_found"`).
- **Logging estructurado** con Serilog (configurable en la sección `Serilog` de `appsettings`).
- **CORS** desde `Cors:Origins` (arreglo en `appsettings` o lista separada por comas en `Cors__Origins`).
- **OpenAPI + Scalar** en `/openapi/v1.json` y `/scalar`, solo en Development.
- **Health checks** en `/health` con respuesta JSON (`status`, `version`, `checks`). La Etapa 01
  agrega el check de la base de datos y la Etapa 04, el del servicio de IA.

### Estructura de carpetas

```
opsynex-ai/
├── .github/workflows/ci.yml       CI: .NET, Python y frontend
├── docker-compose.yml             sqlserver, api, ai-service, frontend
├── .env.example                   variables de entorno de ejemplo
├── backend/
│   ├── Opsynex.sln                abre en Visual Studio 2022+/2026
│   ├── Directory.Build.props      Nullable, analizadores, warnings = errores en Release
│   ├── Directory.Packages.props   versiones centralizadas de NuGet
│   ├── Dockerfile                 build multi-stage (sdk → aspnet)
│   ├── src/Opsynex.Api|Application|Domain|Infrastructure/
│   └── tests/Opsynex.UnitTests|IntegrationTests/
├── ai-service/
│   ├── pyproject.toml · Dockerfile
│   ├── app/ (main.py, config.py, security.py, routers/, engines/, schemas/)
│   └── tests/
├── frontend/
│   ├── package.json · vite.config.ts · tailwind.config.ts · components.json · Dockerfile · nginx.conf
│   └── src/ (styles/tokens.css, lib/, components/ui/, components/<feature>/, pages/, test/)
├── docs/ (especificaciones, diseno, documentacion, Documentacion_Opsynex_AI.docx)
└── scripts/ (generar_documentacion.py, setup_cloud.sh)
```

### Flujo de datos

1. El usuario carga los archivos financieros.
2. El sistema valida la estructura.
3. El sistema normaliza (homologa) las cuentas.
4. Los datos se almacenan en la base de datos.
5. El motor de KPIs ejecuta los cálculos.
6. El motor de diagnóstico evalúa las condiciones.
7. El motor de impacto financiero estima las pérdidas.
8. El dashboard muestra los resultados.

### Seguridad

Requerimientos iniciales: autenticación JWT, encriptación de contraseñas, validación de
entradas, validación de archivos y control de acceso basado en roles.

> Pendiente (Etapas 02 y 12): implementación detallada.
