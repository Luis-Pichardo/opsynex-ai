#!/usr/bin/env bash
# Opsynex AI — prepara un contenedor de Claude Code en la nube (Ubuntu 24.04) para compilar
# y probar todo el monorepo. Es idempotente: puede ejecutarse varias veces.
#
# Uso:
#   bash scripts/setup_cloud.sh            # .NET 10 + dependencias de backend, IA y frontend
#   bash scripts/setup_cloud.sh --with-sql # además arranca Docker y SQL Server 2022 (compose)
#
# Qué hace:
#   1. Instala el .NET 10 SDK si falta (apt de Ubuntu → repo de Microsoft → dotnet-install.sh).
#   2. dotnet restore y dotnet tool restore (dotnet-ef) de backend/.
#   3. Crea ai-service/.venv con Python 3.12 e instala el servicio con extras dev.
#   4. npm ci en frontend/.
#   5. (--with-sql) arranca dockerd si no está corriendo y levanta el servicio sqlserver.

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WITH_SQL=false
for arg in "$@"; do
  case "$arg" in
    --with-sql) WITH_SQL=true ;;
    *) echo "Opción desconocida: $arg" >&2; exit 2 ;;
  esac
done

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DEBIAN_FRONTEND=noninteractive

log() { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }

SUDO=""
if [ "$(id -u)" -ne 0 ] && command -v sudo >/dev/null 2>&1; then SUDO="sudo"; fi

has_dotnet10() {
  command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'
}

# ---------------------------------------------------------------- 1. .NET 10 SDK
log "1/5 .NET 10 SDK"
if has_dotnet10; then
  echo ".NET 10 ya instalado: $(dotnet --version)"
else
  # a) apt de Ubuntu 24.04 (noble-updates publica dotnet-sdk-10.0). Es lo que funcionó el 15/11/2026.
  $SUDO apt-get update -qq || true
  if ! $SUDO apt-get install -y -qq dotnet-sdk-10.0; then
    # b) repositorio de Microsoft
    echo "apt de Ubuntu no tiene dotnet-sdk-10.0; probando packages.microsoft.com"
    tmp_deb="$(mktemp --suffix=.deb)"
    curl -fsSL -o "$tmp_deb" https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb
    $SUDO dpkg -i "$tmp_deb"
    rm -f "$tmp_deb"
    $SUDO apt-get update -qq
    if ! $SUDO apt-get install -y -qq dotnet-sdk-10.0; then
      # c) script oficial dotnet-install.sh
      echo "Probando dotnet-install.sh"
      curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
      bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
      export PATH="$HOME/.dotnet:$PATH"
      grep -q 'HOME/.dotnet' "$HOME/.bashrc" 2>/dev/null || echo 'export PATH="$HOME/.dotnet:$PATH"' >> "$HOME/.bashrc"
    fi
  fi
  has_dotnet10 || { echo "No se pudo instalar el .NET 10 SDK" >&2; exit 1; }
  echo "Instalado: $(dotnet --version)"
fi

# ---------------------------------------------------------------- 2. Backend
log "2/5 dotnet restore y herramientas locales (backend)"
(cd "$ROOT_DIR/backend" && dotnet restore Opsynex.sln && dotnet tool restore)

# ---------------------------------------------------------------- 3. Servicio de IA
log "3/5 Servicio de IA (Python 3.12)"
PYTHON_BIN="$(command -v python3.12 || command -v python3)"
if [ ! -x "$ROOT_DIR/ai-service/.venv/bin/python" ]; then
  "$PYTHON_BIN" -m venv "$ROOT_DIR/ai-service/.venv"
fi
"$ROOT_DIR/ai-service/.venv/bin/pip" install -q --upgrade pip
(cd "$ROOT_DIR/ai-service" && .venv/bin/pip install -q -e ".[dev]")
echo "Entorno virtual: ai-service/.venv ($("$ROOT_DIR/ai-service/.venv/bin/python" --version))"

# ---------------------------------------------------------------- 4. Frontend
log "4/5 Frontend (npm ci)"
(cd "$ROOT_DIR/frontend" && npm ci --no-audit --no-fund)

# ---------------------------------------------------------------- 5. SQL Server (opcional)
log "5/5 SQL Server"
if [ "$WITH_SQL" = true ]; then
  if ! docker info >/dev/null 2>&1; then
    command -v dockerd >/dev/null 2>&1 || { echo "dockerd no está instalado" >&2; exit 1; }
    echo "Arrancando dockerd en segundo plano (log en /tmp/dockerd.log)"
    $SUDO sh -c 'nohup dockerd > /tmp/dockerd.log 2>&1 &'
    for _ in $(seq 1 30); do docker info >/dev/null 2>&1 && break; sleep 1; done
    docker info >/dev/null 2>&1 || { echo "dockerd no arrancó; revisa /tmp/dockerd.log" >&2; exit 1; }
  fi
  (cd "$ROOT_DIR" && docker compose up -d sqlserver)
  echo "Esperando a que SQL Server esté listo…"
  status=""
  for _ in $(seq 1 40); do
    status="$(docker inspect -f '{{.State.Health.Status}}' opsynex-sqlserver-1 2>/dev/null || true)"
    [ "$status" = "healthy" ] && break
    sleep 3
  done
  echo "SQL Server: ${status:-desconocido} (localhost:1433, usuario sa)"
else
  echo "Omitido (usa --with-sql para arrancar Docker y SQL Server 2022)."
fi

log "Listo. Comandos de verificación:"
cat <<'EOF'
  cd backend && dotnet build && dotnet test      # pruebas de BD: SQL Server con Testcontainers (o SQLite sin Docker)
  cd backend && dotnet ef database update -p src/Opsynex.Infrastructure -s src/Opsynex.Api
  cd ai-service && .venv/bin/pytest && .venv/bin/ruff check .
  cd frontend && npm run lint && npm test && npm run build
EOF
