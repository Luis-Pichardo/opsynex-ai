## Instalación y configuración

### Requisitos

| Herramienta | Versión | Para qué |
|---|---|---|
| .NET SDK | 10.0 (LTS) | API (`backend/`) |
| Visual Studio | 2022 17.14+ o 2026 (opcional; también VS Code o Rider) | abrir `backend/Opsynex.sln` |
| SQL Server | 2022 (local, Docker o Azure SQL) | base de datos (desde la Etapa 01) |
| Python | 3.12 | servicio de IA (`ai-service/`) |
| Node.js | 20.19 o superior (recomendado 22) | frontend (`frontend/`) |
| Docker + Docker Compose | recientes | opcional: SQL Server y la plataforma completa |

### Variables de entorno

Copia `.env.example` a `.env` en la raíz. En el API, las claves con `__` equivalen a las
secciones de `appsettings.json` (por ejemplo, `Jwt__Secret` ↔ `Jwt:Secret`). Para desarrollo
local en Visual Studio se recomienda **User Secrets** (`dotnet user-secrets set "Jwt:Secret" "..."`
en `backend/src/Opsynex.Api`). Nunca subas secretos al repositorio.

| Variable | Componente | Descripción | Ejemplo |
|---|---|---|---|
| `ConnectionStrings__Default` | API | Cadena de conexión a SQL Server | `Server=localhost,1433;Database=Opsynex;User Id=sa;Password=…;TrustServerCertificate=True` |
| `Jwt__Secret` | API | Secreto para firmar los tokens (≥ 32 caracteres) | valor aleatorio |
| `Jwt__ExpireMinutes` | API | Duración del token | `60` |
| `Cors__Origins` | API | Orígenes del frontend permitidos (separados por comas) | `http://localhost:5173` |
| `AiService__BaseUrl` | API | URL del servicio de IA | `http://localhost:8001` |
| `AiService__ApiKey` | API | Clave interna compartida con el servicio de IA | valor aleatorio |
| `Upload__MaxMb` | API | Tamaño máximo de archivo a cargar | `20` |
| `Database__Provider` | API | `SqlServer` (normal) o `Sqlite` (solo pruebas) | `SqlServer` |
| `Database__MigrateOnStartup` | API | Aplica migraciones al arrancar (`true` en Development) | `true` |
| `OPSYNEX_TEST_DB` | Pruebas | Vacío = Testcontainers si hay Docker, si no SQLite; `sqlserver` o `sqlite` para forzar | (vacío) |
| `AI_SERVICE_API_KEY` | IA | Misma clave que `AiService__ApiKey` | valor aleatorio |
| `ANTHROPIC_API_KEY` | IA | Clave de la API de Claude (opcional hasta la Etapa 04) | `sk-ant-…` |
| `ANTHROPIC_MODEL` | IA | Modelo de Claude | `claude-opus-5-5` |
| `VITE_API_URL` | Frontend | URL del API vista desde el navegador | `http://localhost:5080` |
| `MSSQL_SA_PASSWORD` | Docker | Contraseña de `sa` del contenedor de SQL Server | `Opsynex_Dev_2026!` |

### Levantar el proyecto en local

```bash
# 1. Base de datos
docker compose up -d sqlserver
cd backend && dotnet tool restore
dotnet ef database update -p src/Opsynex.Infrastructure -s src/Opsynex.Api   # opcional: el API migra al arrancar en Development

# 2. API .NET → http://localhost:5080 (documentación en /scalar, salud en /health)
cd backend
dotnet restore && dotnet build
dotnet run --project src/Opsynex.Api

# 3. Servicio de IA → http://localhost:8001/health
cd ai-service
python3.12 -m venv .venv && source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install -e ".[dev]"
AI_SERVICE_API_KEY=dev-internal-key uvicorn app.main:app --reload --port 8001

# 4. Frontend → http://localhost:5173
cd frontend
npm install
npm run dev
```

**En Visual Studio:** abre `backend/Opsynex.sln`, marca `Opsynex.Api` como proyecto de inicio
y ejecuta el perfil `http` (abre Scalar en el navegador). Las pruebas aparecen en el
Explorador de pruebas.

### Pruebas y linters

```bash
cd backend && dotnet build -c Release && dotnet test         # Release: warnings = errores
OPSYNEX_TEST_DB=sqlite dotnet test                            # sin Docker: SQLite in-memory
cd ai-service && pytest && ruff check . && ruff format --check .
cd frontend && npm run lint && npm test && npm run build
```

### Con Docker (plataforma completa)

```bash
cp .env.example .env
docker compose up -d --build
```

| Servicio | URL |
|---|---|
| Frontend (nginx) | http://localhost:8081 |
| API | http://localhost:8080/health |
| Servicio de IA | http://localhost:8001/health |
| SQL Server | `localhost,1433` (usuario `sa`) |

El API espera a que SQL Server pase su *healthcheck* antes de arrancar. La URL del API se
incrusta en el frontend al construir la imagen (`VITE_API_URL_DOCKER`, por defecto
`http://localhost:8080`).

### En la nube (sesiones de Claude Code web/móvil)

Los contenedores de Claude Code en la nube (Ubuntu 24.04) no traen .NET y no tienen Docker
corriendo. El script `scripts/setup_cloud.sh` deja todo listo y es idempotente:

```bash
bash scripts/setup_cloud.sh            # .NET 10 + dependencias de backend, IA y frontend
bash scripts/setup_cloud.sh --with-sql # además arranca dockerd y SQL Server 2022
```

Qué hace:

1. Instala el .NET 10 SDK con `apt-get install dotnet-sdk-10.0` (paquete de Ubuntu
   `noble-updates`); si no está disponible prueba el repositorio de Microsoft y, por último,
   `dotnet-install.sh`.
2. `dotnet restore` de la solución y `dotnet tool restore` (instala `dotnet-ef`).
3. Crea `ai-service/.venv` con Python 3.12 e instala el servicio con los extras de desarrollo.
4. `npm ci` en el frontend.
5. Con `--with-sql`: arranca `dockerd` en segundo plano y `docker compose up -d sqlserver`,
   y espera a que el contenedor esté *healthy*.

Tarda unos 15 s con la caché caliente y unos 2–3 min en un contenedor nuevo (más la descarga
de la imagen de SQL Server, ~1,5 GB, la primera vez).

Limitación conocida en la nube: `docker build` necesita el certificado del proxy del
contenedor para descargar paquetes. Para probar las imágenes allí se usa
`--network host`, `--build-context ca=/root/.ccr` y una copia temporal del Dockerfile que
instala ese certificado; los Dockerfile del repositorio no se modifican.
