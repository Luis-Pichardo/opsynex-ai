# Etapa 00 — Preparación del entorno y estructura del repositorio

**Depende de:** nada · **Rama:** `etapa-00-preparacion`

## Prompt

> Crea la estructura base del monorepo de Opsynex AI siguiendo `CLAUDE.md`: una solución
> .NET 10 con arquitectura limpia (API ASP.NET Core), un servicio de IA en Python/FastAPI y un
> frontend React + TypeScript + Vite + Tailwind + shadcn/ui. Los tres deben arrancar, tener un
> endpoint o pantalla de salud, tests, linters y CI. Deja el contenedor de la nube preparado
> para compilar y probar todo. No implementes lógica de negocio todavía.

## Tareas

1. **Entorno en la nube primero**
   - Instala el .NET 10 SDK en este contenedor (Ubuntu 24.04). Prueba en este orden: apt con
     el repositorio de Microsoft (`packages.microsoft.com`), apt de Ubuntu (`dotnet-sdk-10.0`)
     y el script `dotnet-install.sh`. Si .NET 10 no está disponible, usa .NET 9 y anótalo.
   - Verifica si puedes levantar SQL Server (daemon de Docker o paquete `mssql-server` de
     packages.microsoft.com). Si no se puede, deja los tests de integración con SQLite
     in-memory y la opción de Testcontainers cuando haya Docker.
   - Escribe `scripts/setup_cloud.sh` (idempotente) con lo que funcionó: instalar .NET, `dotnet
     restore`, `pip install -e ".[dev]"` en `ai-service` y `npm ci` en `frontend`.
   - Documenta todo en `ESTADO.md` → "Notas del entorno".
2. **Raíz**: `.gitignore` (agrega `bin/`, `obj/`, `*.user`, `.vs/`), `.editorconfig`,
   `.env.example` (`ConnectionStrings__Default`, `Jwt__Secret`, `Jwt__ExpireMinutes`,
   `Cors__Origins`, `AiService__BaseUrl`, `AiService__ApiKey`, `ANTHROPIC_API_KEY`,
   `ANTHROPIC_MODEL`, `VITE_API_URL`, `Upload__MaxMb`), `docker-compose.yml` (sqlserver 2022
   con volumen y healthcheck, api, ai-service, frontend) y `README.md` del proyecto.
3. **Backend .NET** (`backend/`)
   - `Opsynex.sln` con `Opsynex.Api`, `Opsynex.Application`, `Opsynex.Domain`,
     `Opsynex.Infrastructure`, `Opsynex.UnitTests` y `Opsynex.IntegrationTests`, con referencias
     según la arquitectura limpia. `Directory.Build.props` con `Nullable`,
     `TreatWarningsAsErrors` en Release, `LangVersion latest` y analizadores.
   - Paquetes centralizados en `Directory.Packages.props`.
   - `Program.cs`: Serilog, ProblemDetails, CORS desde configuración, OpenAPI + Scalar en
     `/scalar` (solo en Development), versionado de rutas `/api/v1`, health checks en `/health`
     (incluye el check de BD a partir de la etapa 1 y el del servicio de IA a partir de la 4).
   - Test de integración de `/health` con `WebApplicationFactory`.
   - `Dockerfile` multi-stage.
4. **Servicio de IA** (`ai-service/`)
   - `pyproject.toml` (fastapi, uvicorn, pydantic-settings, pandas, rapidfuzz, scikit-learn,
     anthropic; extras dev: pytest, httpx, ruff).
   - `app/main.py` con `GET /health`; middleware que exige la cabecera `X-Internal-Api-Key`
     en todo excepto `/health`. Estructura `app/routers/`, `app/engines/`, `app/schemas/` y `tests/`.
   - `Dockerfile`.
5. **Frontend** (`frontend/`)
   - Vite + React 18 + TypeScript estricto + Tailwind. Inicializa **shadcn/ui** (estilo
     "new-york", alias `@/`). Copia `docs/diseno/tokens.css` a `src/styles/tokens.css` y mapea los
     tokens en `tailwind.config` (colores `background`, `surface`, `primary`, `critical`,
     `chart-1..8`, etc.; `borderRadius`, `boxShadow` y `fontFamily` con Inter).
   - ESLint + Prettier (con el plugin de Tailwind). Vitest + Testing Library.
   - Página inicial que llama a `/health` del API y muestra el estado con un `Badge`.
   - `Dockerfile` (build y nginx).
6. **CI**: `.github/workflows/ci.yml` con tres jobs: .NET (build + test), Python (ruff +
   pytest) y frontend (lint + test + build).

## Criterios de aceptación

- [ ] `dotnet build` y `dotnet test` pasan; `GET /health` del API responde 200.
- [ ] `pytest` y `ruff check` pasan en `ai-service`; su `/health` responde 200.
- [ ] `npm run lint`, `npm test` y `npm run build` pasan; la página muestra el estado del API.
- [ ] `scripts/setup_cloud.sh` existe, se probó en este contenedor y quedó documentado en `ESTADO.md`.
- [ ] La solución abre en Visual Studio (`backend/Opsynex.sln`).

## Documentación a actualizar

- `02_arquitectura.md`: diagrama de componentes, estructura de carpetas y stack con versiones exactas.
- `08_instalacion.md`: requisitos (Visual Studio 2022+/.NET 10, SQL Server, Python 3.12,
  Node 20+), variables de entorno y cómo levantar todo local, con Docker y en la nube.
- `13_bitacora.md`.
