# Servicio de IA de Opsynex AI

Servicio interno y *stateless* en Python 3.12 + FastAPI. Solo lo llama el API .NET
(cabecera `X-Internal-Api-Key`); no accede a la base de datos.

```bash
python3.12 -m venv .venv && source .venv/bin/activate
pip install -e ".[dev]"
AI_SERVICE_API_KEY=dev-key uvicorn app.main:app --reload --port 8001
pytest && ruff check . && ruff format --check .
```

Variables: `AI_SERVICE_API_KEY` (igual a `AiService__ApiKey` del API), `ANTHROPIC_API_KEY`,
`ANTHROPIC_MODEL` (por defecto `claude-opus-5-5`), `LOG_LEVEL`.
