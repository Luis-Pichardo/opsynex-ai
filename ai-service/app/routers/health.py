"""Endpoint de salud (público, sin clave interna)."""

from fastapi import APIRouter

from app import __version__
from app.config import get_settings
from app.schemas.health import HealthResponse

router = APIRouter(tags=["health"])


@router.get("/health", response_model=HealthResponse)
async def health() -> HealthResponse:
    """Indica si el servicio está en ejecución y si Claude está configurado."""
    settings = get_settings()
    return HealthResponse(
        status="Healthy",
        service="opsynex-ai-service",
        version=__version__,
        claude_configured=bool(settings.anthropic_api_key),
        model=settings.anthropic_model,
    )
