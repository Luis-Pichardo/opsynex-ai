# syntax=docker/dockerfile:1
# Servicio de IA de Opsynex AI (FastAPI). Contexto de build: carpeta ai-service/.

FROM python:3.12-slim
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    UVICORN_WORKERS=2
WORKDIR /app

COPY pyproject.toml README.md ./
COPY app/ app/
RUN pip install . && useradd --create-home --uid 10001 appuser

USER appuser
EXPOSE 8001
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD ["python", "-c", "import urllib.request; urllib.request.urlopen('http://localhost:8001/health')"]
# Varios procesos (UVICORN_WORKERS): las llamadas a Claude son largas y no deben bloquear el resto.
CMD ["sh", "-c", "exec uvicorn app.main:app --host 0.0.0.0 --port 8001 --workers ${UVICORN_WORKERS} --no-server-header"]
