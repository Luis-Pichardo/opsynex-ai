"""Punto de entrada del servicio de IA (FastAPI)."""

import logging

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

from app import __version__
from app.config import get_settings
from app.correlation import CorrelationIdFilter, CorrelationIdMiddleware
from app.llm.client import LlmError
from app.routers import health, homologation, insights, system
from app.security import InternalApiKeyMiddleware


def create_app() -> FastAPI:
    """Construye la aplicación FastAPI."""
    settings = get_settings()
    logging.basicConfig(
        level=settings.log_level.upper(),
        format="%(asctime)s %(levelname)s [%(correlation_id)s] %(name)s: %(message)s",
    )
    for handler in logging.getLogger().handlers:
        if not any(isinstance(f, CorrelationIdFilter) for f in handler.filters):
            handler.addFilter(CorrelationIdFilter())

    app = FastAPI(
        title="Opsynex AI — Servicio de IA",
        version=__version__,
        description="Servicio interno y stateless. Solo lo llama el API .NET.",
    )
    app.add_middleware(InternalApiKeyMiddleware)
    # Se agrega al final para ejecutarse primero: también las respuestas 401 llevan el id.
    app.add_middleware(CorrelationIdMiddleware)
    app.include_router(health.router)
    app.include_router(system.router)
    app.include_router(homologation.router)
    app.include_router(insights.router)

    @app.exception_handler(LlmError)
    async def llm_error(_: Request, error: LlmError) -> JSONResponse:
        # Mismo formato ProblemDetails que el API .NET, con un `code` propio.
        return JSONResponse(
            status_code=error.status,
            media_type="application/problem+json",
            content={
                "type": "about:blank",
                "title": "Error de la IA generativa",
                "status": error.status,
                "detail": error.message,
                "code": error.code,
            },
        )

    return app


app = create_app()
