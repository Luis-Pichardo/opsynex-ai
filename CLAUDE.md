# Opsynex AI — Instrucciones para Claude Code

Este archivo se carga automáticamente en cada sesión. Léelo completo antes de trabajar.

## Qué es el proyecto

Opsynex AI es una plataforma SaaS de diagnóstico financiero-operacional para empresas
manufactureras y de servicios. Recibe archivos Excel/CSV con estados financieros y datos
operacionales, homologa las cuentas contables, calcula KPIs, detecta ineficiencias por reglas,
cuantifica su impacto económico, calcula un score de salud (0-100) y lo muestra en dashboards.

Especificaciones originales del cliente (fuente de verdad funcional):
- `docs/especificaciones/ESPECIFICACIONES_DEL_SISTEMA.pdf` (+ `.txt`) — modelo financiero **anual**.
- `docs/especificaciones/PLATAFORMA_DE_ANALISIS.pdf` (+ `.txt`) — módulo **industrial** (producción por máquina/línea, OEE, scrap, downtime).

Decisión de alcance: el núcleo del MVP usa el modelo financiero anual (Etapas 3-7, 9-10) y el
módulo industrial se implementa como extensión en la Etapa 11. Ambos comparten usuarios,
empresas, diagnóstico, impacto y score.

## Stack (obligatorio)

| Capa | Tecnología |
|---|---|
| Backend | Python 3.12 + FastAPI, SQLAlchemy 2.x, Alembic, Pydantic v2, pandas, openpyxl |
| Base de datos | PostgreSQL 16 (SQLite en memoria solo para tests) |
| Auth | JWT (python-jose o PyJWT) + bcrypt (passlib) |
| Frontend | React 18 + Vite + TypeScript + Tailwind CSS, React Router, TanStack Query, Recharts |
| Tests | pytest + httpx (backend), Vitest + Testing Library (frontend) |
| Infra | Docker + docker-compose; despliegue en AWS o GCP (Etapa 13) |

## Estructura del repositorio

```
opsynex-ai/
├── CLAUDE.md                  ← este archivo
├── ESTADO.md                  ← estado de cada etapa (actualizar siempre)
├── etapas/                    ← un archivo por etapa con su prompt y criterios
├── backend/                   ← FastAPI (se crea en Etapa 0)
├── frontend/                  ← React (se crea en Etapa 0)
├── docs/
│   ├── especificaciones/      ← PDFs del cliente (no modificar)
│   ├── documentacion/         ← fuentes Markdown de la documentación
│   └── Documentacion_Opsynex_AI.docx  ← documento Word generado
└── scripts/
    └── generar_documentacion.py
```

## Protocolo de trabajo por etapas (OBLIGATORIO)

Cuando el usuario diga algo como **"continúa con la etapa N"**, **"haz la etapa N"** o
**"sigue con la siguiente etapa"**:

1. Lee `ESTADO.md` para ver qué etapas están completas. Si pide "la siguiente", toma la
   primera etapa pendiente. Si una etapa previa requerida no está completa, avísale antes de seguir.
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
`ESTADO.md` en 🟡 con lo que falta, y continúa en la siguiente sesión.

## Documentación (Word) — se actualiza al cerrar CADA etapa

El documento Word `docs/Documentacion_Opsynex_AI.docx` se **genera** a partir de los Markdown
de `docs/documentacion/`. Nunca edites el `.docx` a mano.

1. Edita los `.md` correspondientes en `docs/documentacion/` (cada etapa indica cuáles).
   Reemplaza los textos `> Pendiente (Etapa N)` por contenido real.
2. Agrega una entrada en `docs/documentacion/12_bitacora.md` con: etapa, fecha, resumen,
   archivos principales, decisiones técnicas y pendientes.
3. Sube la versión en `docs/documentacion/metadata.yaml` (`version: 0.N.0`).
4. Regenera el Word:
   ```bash
   python3 scripts/generar_documentacion.py
   ```
5. Incluye el `.docx` y los `.md` en el commit de la etapa.

La documentación tiene dos partes: **del proyecto** (alcance, objetivos, guía de usuario,
bitácora) y **técnica** (arquitectura, modelo de datos, API, motores, instalación, pruebas,
despliegue). Escríbela en español, clara para el cliente y para otro desarrollador.

## Convenciones de código

- Código, nombres de variables, tablas y endpoints en **inglés**; comentarios, docs, commits
  y mensajes al usuario en **español**.
- Backend por capas: `routers/` → `services/` → `repositories/` / `models/`. Los motores
  analíticos (KPIs, diagnóstico, impacto, score) son funciones puras en `app/engines/`, sin
  acceso a BD, para poder probarlas fácilmente.
- Toda división debe protegerse contra cero/None (KPIs devuelven `None` si no se pueden calcular).
- Montos con `Decimal`/`Numeric(18,2)` en BD; ratios como float con 4 decimales.
- Multi-empresa: todos los datos pertenecen a una `company_id`; un usuario no Admin solo ve
  datos de su empresa.
- Configuración por variables de entorno (`.env`, ver `.env.example`). Nunca subas secretos.
- Cada endpoint y cada motor nuevo lleva tests. Antes de cerrar una etapa: `pytest` y
  `npm test` en verde, y `ruff` / `eslint` sin errores.

## Comandos habituales

```bash
docker compose up -d db                             # PostgreSQL local
cd backend && pip install -e ".[dev]" && alembic upgrade head
cd backend && uvicorn app.main:app --reload         # API en :8000 (docs en /docs)
cd backend && pytest
cd frontend && npm install && npm run dev           # UI en :5173
cd frontend && npm test
python3 scripts/generar_documentacion.py            # regenera el Word
```

En la nube (sesiones de Claude Code web/móvil) puede no haber Docker; usa PostgreSQL
instalado en el contenedor o SQLite para pruebas, según lo indique `ESTADO.md`.
