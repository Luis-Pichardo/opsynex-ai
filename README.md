# Opsynex AI

Plataforma SaaS de diagnóstico financiero-operacional para empresas manufactureras y de
servicios: recibe estados financieros y datos operacionales en Excel/CSV, homologa las cuentas,
calcula KPIs, detecta ineficiencias, cuantifica su impacto económico y calcula un score de
salud (0–100) que se muestra en dashboards. Un servicio de IA complementa el análisis.

## Componentes

| Carpeta | Tecnología | Puerto local |
|---|---|---|
| `backend/` | C# / ASP.NET Core (.NET 10), EF Core, SQL Server 2022 | 5080 (`dotnet run`) · 8080 (Docker) |
| `ai-service/` | Python 3.12 + FastAPI, Claude (Anthropic) | 8001 |
| `frontend/` | React 18 + TypeScript + Vite + Tailwind + shadcn/ui | 5173 (`npm run dev`) · 8081 (Docker) |

```
[React SPA] ──HTTPS/JWT──► [ASP.NET Core API] ──EF Core──► [SQL Server]
                                  │
                                  └──HTTP interno (X-Internal-Api-Key)──► [Servicio IA] ──► Claude API
```

## Inicio rápido

```bash
cp .env.example .env
docker compose up -d sqlserver                                   # base de datos

cd backend && dotnet run --project src/Opsynex.Api              # http://localhost:5080/scalar
cd ai-service && python3.12 -m venv .venv && .venv/bin/pip install -e ".[dev]" \
  && AI_SERVICE_API_KEY=dev-internal-key .venv/bin/uvicorn app.main:app --reload --port 8001
cd frontend && npm install && npm run dev                       # http://localhost:5173
```

Todo con Docker: `docker compose up -d --build` (frontend en http://localhost:8081).

En una sesión de Claude Code en la nube: `bash scripts/setup_cloud.sh --with-sql`.

## Pruebas

```bash
cd backend && dotnet test
cd ai-service && .venv/bin/pytest && .venv/bin/ruff check .
cd frontend && npm run lint && npm test && npm run build
```

## Documentación

- Instalación detallada: `docs/documentacion/08_instalacion.md`.
- Arquitectura: `docs/documentacion/02_arquitectura.md`.
- Documento completo (Word, generado): `docs/Documentacion_Opsynex_AI.docx`.
- Guía de diseño de interfaz: `docs/diseno/GUIA_DE_DISENO.md`.
- Plan por etapas y estado: `etapas/` y `ESTADO.md`. Instrucciones para Claude Code: `CLAUDE.md`.
