# Opsynex AI — Instrucciones para Claude Code

Este archivo se carga automáticamente en cada sesión. Léelo completo antes de trabajar.

## Qué es el proyecto

Opsynex AI es una plataforma SaaS de diagnóstico financiero-operacional para empresas
manufactureras y de servicios. Recibe archivos Excel/CSV con estados financieros y datos
operacionales, homologa las cuentas contables, calcula KPIs, detecta ineficiencias por reglas,
cuantifica su impacto económico, calcula un score de salud (0-100) y lo muestra en dashboards.
Un servicio de IA en Python complementa el análisis (sugerencias de homologación, detección de
anomalías, recomendaciones y resúmenes ejecutivos).

Especificaciones originales del cliente (fuente de verdad funcional):
- `docs/especificaciones/ESPECIFICACIONES_DEL_SISTEMA.pdf` (+ `.txt`) — modelo financiero **anual**.
- `docs/especificaciones/PLATAFORMA_DE_ANALISIS.pdf` (+ `.txt`) — módulo **industrial** (producción por máquina/línea, OEE, scrap, downtime).

Decisión de alcance: el núcleo del MVP usa el modelo financiero anual (etapas 3–10) y el
módulo industrial es una extensión (etapa 12). Ambos comparten usuarios, empresas,
diagnóstico, impacto y score.

> Nota: los PDFs sugieren Python + FastAPI como backend, pero el cliente permite cualquier
> stack. **Por decisión del desarrollador**, el backend principal es C# / ASP.NET Core con
> SQL Server, y Python se usa solo para el servicio de IA.

## Stack (obligatorio)

| Capa | Tecnología |
|---|---|
| Backend principal (API) | **C# / .NET 10 (LTS) / ASP.NET Core Web API**, Entity Framework Core 10, FluentValidation, Serilog, OpenAPI + Scalar |
| Autenticación | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`) + hash de contraseñas con `PasswordHasher<T>` de ASP.NET Core Identity |
| Lectura de archivos | ClosedXML (Excel) y CsvHelper (CSV) |
| Base de datos | **SQL Server 2022** (en tests: Testcontainers si hay Docker; si no, SQLite in-memory de EF Core) |
| Servicio de IA | **Python 3.12 + FastAPI**, pandas, scikit-learn, rapidfuzz, SDK `anthropic` (Claude) |
| Frontend | **React 18 + TypeScript + Vite + Tailwind CSS + shadcn/ui** (Radix), lucide-react, Recharts, TanStack Query, TanStack Table, React Router, React Hook Form + Zod, sonner |
| Tests | xUnit + Shouldly + `WebApplicationFactory` (.NET); pytest + httpx (IA); Vitest + Testing Library (frontend); Playwright (E2E) |
| Infra | Docker + docker-compose; despliegue en Azure, AWS o GCP (se decide en la etapa 14) |

## Arquitectura

```
[React SPA] ──HTTPS/JWT──► [ASP.NET Core API] ──EF Core──► [SQL Server]
                                  │
                                  └──HTTP interno (X-Internal-Api-Key)──► [Servicio IA Python/FastAPI] ──► Claude API
```

- El **API .NET** es dueño de los datos, la seguridad y la lógica de negocio determinista:
  carga, validación, homologación (persistencia), KPIs, diagnóstico por reglas, impacto y score.
- El **servicio de IA** es *stateless*: no accede a la base de datos. Recibe JSON del API y
  devuelve sugerencias (mapeo de cuentas, anomalías, recomendaciones y resúmenes en lenguaje
  natural). **Si no está disponible, el API debe seguir funcionando** (con fallbacks y timeouts).
- El frontend **solo** habla con el API .NET, nunca con el servicio de IA.

## Estructura del repositorio

```
opsynex-ai/
├── CLAUDE.md                     ← este archivo
├── ESTADO.md                     ← estado de cada etapa (actualizar siempre)
├── etapas/                       ← un archivo por etapa con su prompt y criterios
├── backend/                      ← solución .NET (se crea en la etapa 0)
│   ├── Opsynex.sln
│   ├── src/
│   │   ├── Opsynex.Api/            ← controllers, middleware, auth, Program.cs
│   │   ├── Opsynex.Application/    ← servicios, DTOs, validadores, interfaces
│   │   ├── Opsynex.Domain/         ← entidades, enums, motores puros (KPIs, reglas, impacto, score)
│   │   └── Opsynex.Infrastructure/ ← EF Core, repositorios, parsers Excel/CSV, cliente IA
│   └── tests/
│       ├── Opsynex.UnitTests/
│       └── Opsynex.IntegrationTests/
├── ai-service/                   ← servicio de IA en Python (FastAPI)
├── frontend/                     ← React + Vite + shadcn/ui
├── docs/
│   ├── especificaciones/         ← PDFs del cliente (no modificar)
│   ├── diseno/                   ← GUIA_DE_DISENO.md, tokens.css, guia_visual.html
│   ├── documentacion/            ← fuentes Markdown de la documentación
│   └── Documentacion_Opsynex_AI.docx  ← documento Word generado
└── scripts/
    ├── generar_documentacion.py
    └── setup_cloud.sh            ← (etapa 0) configuración del contenedor en la nube
```

## Protocolo de trabajo por etapas (OBLIGATORIO)

Cuando el usuario diga algo como **"continúa con la etapa N"**, **"haz la etapa N"** o
**"sigue con la siguiente etapa"**:

1. Lee `ESTADO.md` para ver qué etapas están completas. Si pide "la siguiente", toma la
   primera etapa pendiente. Si falta una etapa previa requerida, avísale antes de seguir.
2. Lee `etapas/ETAPA_NN_*.md` completo y ejecuta su sección **Prompt** como tu tarea.
3. Crea una rama `etapa-NN-nombre-corto` desde `main` actualizado.
4. Implementa las tareas. Trabaja en commits pequeños con mensajes en español.
5. Ejecuta las pruebas y verifica **todos** los criterios de aceptación de la etapa.
   No marques la etapa como completa si algún criterio falla: repórtalo.
6. **Actualiza la documentación** (ver sección siguiente). Es parte de la etapa, no opcional.
7. Actualiza `ESTADO.md`: estado ✅, fecha, rama/PR y notas breves.
8. Haz push de la rama y abre un Pull Request hacia `main` con el resumen de la etapa
   (qué se hizo, cómo probarlo, criterios cumplidos). El usuario revisa y hace merge,
   normalmente desde la app móvil.
9. Termina con un resumen corto en español: qué se hizo, qué probar y cuál es la siguiente etapa.

Si una etapa es muy grande para una sola sesión, completa las tareas en orden, deja
`ESTADO.md` en 🟡 con lo que falta y continúa en la siguiente sesión.

## Documentación (Word): se actualiza al cerrar CADA etapa

El documento Word `docs/Documentacion_Opsynex_AI.docx` se **genera** a partir de los Markdown
de `docs/documentacion/`. Nunca edites el `.docx` a mano.

1. Edita los `.md` correspondientes en `docs/documentacion/` (cada etapa indica cuáles).
   Reemplaza los textos `> Pendiente (Etapa N)` por contenido real.
2. Agrega una entrada en `docs/documentacion/13_bitacora.md` con: etapa, fecha, resumen,
   archivos principales, decisiones técnicas y pendientes.
3. Sube la versión en `docs/documentacion/metadata.yaml` (`version: 0.N.0`).
4. Regenera el Word: `python3 scripts/generar_documentacion.py` (requiere pandoc y python-docx).
5. Incluye el `.docx` y los `.md` en el commit de la etapa.

La documentación tiene dos partes: **del proyecto** (alcance, objetivos, guía de usuario,
bitácora) y **técnica** (arquitectura, modelo de datos, API, motores, IA, instalación, pruebas,
despliegue). Escríbela en español, clara para el cliente y para otro desarrollador.

## Diseño de interfaz (OBLIGATORIO en todo trabajo de frontend)

Antes de crear o modificar cualquier pantalla o componente, lee
`docs/diseno/GUIA_DE_DISENO.md`. Reglas clave:
- Solo se usan tokens (`frontend/src/styles/tokens.css`, copia de `docs/diseno/tokens.css`)
  mediante clases de Tailwind. **Nada de colores hex, tamaños o sombras sueltos en componentes.**
- Colores semánticos (severidad, score, tendencias) solo con su significado, y siempre con
  icono o texto, nunca el color solo.
- Gráficos con la paleta `--chart-1…8` en orden fijo, sin ejes Y dobles, con tooltip y leyenda.
- Formatos de números, monedas y fechas solo a través de `src/lib/format.ts`.
- Cada pantalla debe funcionar en 375 px y en modo claro y oscuro.
Si una decisión de diseño no está cubierta por la guía, agrégala a la guía en el mismo PR.

## Convenciones de código

**General**
- Código, nombres de clases, tablas y endpoints en **inglés**; comentarios, docs, commits
  y mensajes al usuario en **español**.
- Configuración por variables de entorno / `appsettings` + User Secrets. Nunca subas secretos.
- Cada endpoint y cada motor nuevo lleva tests. Antes de cerrar una etapa deben pasar
  `dotnet build` (sin warnings nuevos), `dotnet test`, `pytest`, `ruff check`, `npm test`,
  `npm run lint` y `npm run build` en lo que aplique.

**.NET**
- Arquitectura limpia: `Api → Application → Domain`; `Infrastructure` implementa interfaces de
  `Application`. `Domain` no depende de nada.
- Los motores analíticos (KPIs, reglas de diagnóstico, impacto, score) son **clases puras en
  `Opsynex.Domain/Engines`**, sin EF ni HTTP, para probarlos fácilmente.
- Montos con `decimal` (`decimal(18,2)` en SQL); ratios con `decimal(9,4)`. Toda división se
  protege contra cero o nulos (el KPI devuelve `null` si no se puede calcular).
- `Nullable` habilitado, `async/await` de punta a punta, `CancellationToken` en los endpoints.
- Errores con `ProblemDetails` (RFC 7807) y un `code` propio.
- Multi-empresa: todas las entidades de datos tienen `CompanyId`; se aplica un filtro global
  de EF Core por empresa para usuarios no Admin.

**Python (ai-service)**
- FastAPI + Pydantic v2, tipado estricto, `ruff` para lint y formato. Funciones puras en
  `app/engines/`, endpoints en `app/routers/`.
- Llamadas a Claude con el SDK oficial `anthropic`; el modelo se configura por variable
  (`ANTHROPIC_MODEL`, por defecto `claude-opus-5-5`). Antes de escribir código de Claude API,
  usa la skill `claude-api`.

**Frontend**
- TypeScript estricto. Componentes de shadcn/ui en `src/components/ui/`; componentes del
  dominio en `src/components/<feature>/`; páginas en `src/pages/`.

## Comandos habituales

```bash
docker compose up -d sqlserver                       # SQL Server local
cd backend && dotnet restore && dotnet build
cd backend && dotnet ef database update -p src/Opsynex.Infrastructure -s src/Opsynex.Api
cd backend && dotnet run --project src/Opsynex.Api   # API (docs OpenAPI en /scalar)
cd backend && dotnet test
cd ai-service && pip install -e ".[dev]" && uvicorn app.main:app --reload --port 8001
cd ai-service && pytest
cd frontend && npm install && npm run dev            # UI en :5173
python3 scripts/generar_documentacion.py             # regenera el Word
```

En la nube (sesiones de Claude Code web/móvil) puede no haber Docker ni SQL Server. Revisa
`ESTADO.md` → "Notas del entorno" para saber cómo se ejecutan allí la BD y los tests.
