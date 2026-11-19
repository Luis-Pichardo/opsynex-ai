"""Punto de entrada del servicio de IA (FastAPI)."""

import logging

from fastapi import FastAPI

from app import __version__
from app.config import get_settings
from app.routers import health, homologation, system
from app.security import InternalApiKeyMiddleware


def create_app() -> FastAPI:
    """Construye la aplicación FastAPI."""
    settings = get_settings()
    logging.basicConfig(
        level=settings.log_level.upper(),
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )

    app = FastAPI(
        title="Opsynex AI — Servicio de IA",
        version=__version__,
        description="Servicio interno y stateless. Solo lo llama el API .NET.",
    )
    app.add_middleware(InternalApiKeyMiddleware)
    app.include_router(health.router)
    app.include_router(system.router)
    app.include_router(homologation.router)
    return app


app = create_app()
