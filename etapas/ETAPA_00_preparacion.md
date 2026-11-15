# Etapa 00 — Preparación del entorno y estructura del repositorio

**Depende de:** nada · **Rama:** `etapa-00-preparacion`

## Prompt

> Crea la estructura base del monorepo de Opsynex AI siguiendo `CLAUDE.md`. Deja un backend
> FastAPI y un frontend React + Vite + TypeScript + Tailwind que arranquen, se comuniquen
> (endpoint de salud) y tengan tests, linters y CI funcionando. No implementes lógica de
> negocio todavía.

## Tareas

1. **Raíz del repo**
   - `.gitignore` (Python, Node, `.env`, `__pycache__`, `node_modules`, `dist`, `.venv`).
   - `.env.example` con: `DATABASE_URL`, `JWT_SECRET`, `JWT_EXPIRE_MINUTES`, `CORS_ORIGINS`,
     `VITE_API_URL`, `MAX_UPLOAD_MB`.
   - `docker-compose.yml` con servicios `db` (postgres:16, volumen persistente, healthcheck),
     `backend` y `frontend` (para desarrollo).
   - `README.md` del proyecto: qué es, cómo levantarlo localmente, enlace a `etapas/README.md`.
2. **Backend** (`backend/`)
   - `pyproject.toml` con dependencias del stack y extras `[dev]` (pytest, httpx, ruff, pytest-cov).
   - Estructura: `app/main.py`, `app/core/config.py` (pydantic-settings), `app/core/database.py`,
     `app/api/routers/`, `app/services/`, `app/repositories/`, `app/models/`, `app/schemas/`,
     `app/engines/`, `tests/`.
   - `GET /health` → `{"status": "ok", "version": "..."}`. CORS configurado desde `.env`.
   - Prefijo de API `/api/v1`.
   - `Dockerfile` del backend.
   - Test de `/health` en `tests/test_health.py`.
3. **Frontend** (`frontend/`)
   - Vite + React 18 + TypeScript + Tailwind CSS configurado. ESLint + Prettier.
   - Estructura: `src/api/`, `src/components/`, `src/pages/`, `src/hooks/`, `src/lib/`, `src/types/`.
   - Página inicial simple con el nombre "Opsynex AI" que llama a `/health` y muestra el estado.
   - Vitest + Testing Library con un test de ejemplo.
   - `Dockerfile` del frontend.
4. **CI**: `.github/workflows/ci.yml` que en cada PR ejecute: ruff + pytest (backend) y
   eslint + vitest + build (frontend).
5. **Entorno en la nube**: verifica si en este contenedor hay Docker o PostgreSQL. Documenta en
   `ESTADO.md` → "Notas del entorno" cómo levantar la BD aquí (por ejemplo `apt-get install
   postgresql` o usar SQLite para tests). Escribe `scripts/setup_cloud.sh` con los pasos que
   funcionaron (instalar dependencias del backend y frontend), para usarlo como script de
   configuración del entorno de Claude Code en la nube.

## Criterios de aceptación

- [ ] `uvicorn app.main:app` arranca y `GET /health` responde 200.
- [ ] `npm run dev` arranca y la página muestra el estado del backend.
- [ ] `pytest`, `ruff check`, `npm test`, `npm run lint` y `npm run build` pasan.
- [ ] El workflow de CI existe y es válido.
- [ ] `scripts/setup_cloud.sh` existe y fue probado en este contenedor.

## Documentación a actualizar

- `02_arquitectura.md`: estructura de carpetas, stack con versiones, diagrama de componentes (texto).
- `07_instalacion.md`: requisitos, variables de entorno, cómo levantar todo local y en la nube.
- `12_bitacora.md`: entrada de la Etapa 00.
