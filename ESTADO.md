# Estado del proyecto Opsynex AI

Leyenda: ⬜ pendiente · 🟡 en progreso · ✅ completada

| # | Etapa | Archivo | Estado | Fecha | PR / Notas |
|---|---|---|---|---|---|
| 00 | Preparación del entorno y estructura | `etapas/ETAPA_00_preparacion.md` | ✅ | 15/11/2026 | Rama `ccr-428b3ea1-sne2o0` (PR a `main`). Monorepo, salud, tests, Docker y CI. |
| 01 | Base de datos y modelos (EF Core + SQL Server) | `etapas/ETAPA_01_base_de_datos.md` | ✅ | 15/11/2026 | Rama `etapa-01-base-de-datos` (PR apilado sobre el de la etapa 00). 10 tablas, migración `InitialCreate`, filtro por empresa, repositorios; pruebas con SQL Server real (Testcontainers). |
| 02 | Autenticación y roles | `etapas/ETAPA_02_autenticacion.md` | ✅ | 18/11/2026 | Rama `etapa-02-autenticacion`. JWT, bloqueo, roles y aislamiento por empresa, CRUD de empresas/usuarios (solo Admin), seed del Admin; 163 pruebas .NET. |
| 03 | Carga y validación de datos | `etapas/ETAPA_03_carga_de_datos.md` | ✅ | 18/11/2026 | Rama `etapa-03-carga-de-datos` (PR apilado sobre el de la etapa 02). Parser del libro contable real, 9 validaciones de calidad, `/uploads`, `/data`, plantillas; 19 libros sintéticos; 322 pruebas .NET. |
| 04 | Homologación contable + servicio de IA | `etapas/ETAPA_04_homologacion.md` | ✅ | 18/11/2026 | Rama `etapa-04-homologacion` (PR apilado sobre el de la etapa 03). Homologación automática con IA y fallback, consolidación sin doble conteo, revisión opcional; 355 pruebas .NET y 98 de Python. |
| 05 | Motor de KPIs | `etapas/ETAPA_05_motor_kpis.md` | ⬜ | | |
| 06 | Motor de diagnóstico | `etapas/ETAPA_06_motor_diagnostico.md` | ⬜ | | |
| 07 | Impacto financiero y score | `etapas/ETAPA_07_impacto_y_score.md` | ⬜ | | |
| 08 | Frontend base y sistema de diseño | `etapas/ETAPA_08_frontend_base.md` | ⬜ | | |
| 09 | Frontend: carga de datos y homologación | `etapas/ETAPA_09_frontend_carga.md` | ⬜ | | |
| 10 | Dashboard ejecutivo y problemas (cierre MVP) | `etapas/ETAPA_10_dashboard.md` | ⬜ | | |
| 11 | Inteligencia artificial: recomendaciones, anomalías y resumen | `etapas/ETAPA_11_inteligencia_artificial.md` | ⬜ | | |
| 12 | Módulo industrial | `etapas/ETAPA_12_modulo_industrial.md` | ⬜ | | |
| 13 | Seguridad, calidad y datos demo | `etapas/ETAPA_13_calidad_y_demo.md` | ⬜ | | |
| 14 | Despliegue en la nube | `etapas/ETAPA_14_despliegue.md` | ⬜ | | |

**Hito MVP:** etapas 00–10 completas (cumple la "Definición MVP" de las especificaciones).

## Notas del entorno

Revisado el 15/11/2026 (contenedor de Claude Code en la nube, Ubuntu 24.04, usuario root).

- **Preparar el contenedor:** `bash scripts/setup_cloud.sh --with-sql` (idempotente, ~15 s con
  caché; 2–3 min en un contenedor nuevo). Sin `--with-sql` no arranca Docker ni SQL Server.
- **.NET:** el contenedor no trae .NET. Funciona `apt-get install -y dotnet-sdk-10.0` (paquete
  de Ubuntu noble-updates, SDK 10.0.115 / runtime 10.0.15). El repositorio de
  `packages.microsoft.com` para 24.04 no publica .NET 10 (el script lo intenta como respaldo y
  luego `dotnet-install.sh`).
- **Docker:** el daemon no corre al iniciar, pero `dockerd` está instalado y **sí arranca**
  (`nohup dockerd > /tmp/dockerd.log 2>&1 &`). `mcr.microsoft.com` responde, así que SQL Server
  2022 corre en contenedor: `docker compose up -d sqlserver` (sa / `Opsynex_Dev_2026!`,
  `localhost,1433`). Por lo tanto las pruebas de integración con BD (Etapa 01) pueden usar
  **Testcontainers**; SQLite in-memory queda como respaldo si no hay Docker.
- **docker build en la nube:** los contenedores de build no confían en el certificado del
  proxy. Para probar imágenes: `--network host`, `--build-context ca=/root/.ccr`, pasar
  `HTTPS_PROXY` como build-arg y usar una copia temporal del Dockerfile que copie
  `ca-bundle.crt` y defina `SSL_CERT_FILE`/`NODE_EXTRA_CA_CERTS`/`PIP_CERT`. Así se verificó
  que las tres imágenes construyen y responden `/health`. No modificar los Dockerfile del repo.
- **Base de datos en las pruebas (etapa 01):** `dotnet test` levanta SQL Server 2022 con
  Testcontainers (necesita `dockerd` corriendo: `setup_cloud.sh --with-sql`); funciona en la
  nube. Sin Docker cae automáticamente a SQLite in-memory (`OPSYNEX_TEST_DB=sqlite` lo fuerza).
  La suite completa tarda ~25 s (la mayor parte es el arranque del contenedor).
- **Migraciones:** `cd backend && dotnet tool restore` y luego
  `dotnet ef database update -p src/Opsynex.Infrastructure -s src/Opsynex.Api` (usa el SQL
  Server del compose). En Development el API también migra al arrancar.
- **Autenticación (etapa 02):** para probar a mano, crea el Admin con
  `Seed__AdminEmail=admin@opsynex.local Seed__AdminPassword=Admin12345 dotnet run --project src/Opsynex.Api -- seed-admin`
  (desde `backend/`) y luego `POST /api/v1/auth/login`. En Development el API usa el
  `Jwt:Secret` de `appsettings.Development.json`; en las pruebas, `TestJwt.Secret`.
- **Libros sintéticos (etapa 03):** `python3 backend/tests/TestData/generate_sample_workbooks.py`
  regenera `backend/sample-data/` (usa openpyxl y LibreOffice `soffice`, ambos presentes en el
  contenedor; tarda ~20 s). Las pruebas .NET copian esa carpeta a su salida: después de
  regenerar, haz `dotnet build` antes de `dotnet test --no-build`.
- **Servicio de IA en las pruebas (etapa 04):** las pruebas de homologación de punta a punta
  arrancan el servicio real con `ai-service/.venv/bin/python -m uvicorn` (lo crea
  `setup_cloud.sh`). Sin ese venv se omiten; con `OPSYNEX_REQUIRE_AI=true` fallan en lugar de
  omitirse (así está en la CI).
- **Python:** usar `python3.12` (el `python3` del sistema es 3.13). El venv vive en
  `ai-service/.venv`; ejecutar `ai-service/.venv/bin/pytest` y `.venv/bin/ruff`.
- **Node:** v22 y npm 10 instalados. Frontend con `npm ci`.
- **Word:** `python3 scripts/generar_documentacion.py` funciona (pandoc y python-docx presentes).
- **Variables necesarias para desarrollo:** ninguna obligatoria todavía; `.env.example` tiene
  los valores por defecto. `ANTHROPIC_API_KEY` solo hará falta desde la Etapa 04/11.
- **Ramas:** las sesiones en la nube trabajan en la rama asignada por el entorno (p. ej.
  `ccr-…`) en lugar de `etapa-NN-…`; el PR va igualmente a `main`.
