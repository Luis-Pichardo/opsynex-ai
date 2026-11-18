"""Endpoints de diagnóstico interno (protegidos por la clave interna)."""

from fastapi import APIRouter

from app import __version__

router = APIRouter(prefix="/v1", tags=["system"])


@router.get("/ping")
async def ping() -> dict[str, str]:
    """Permite al API .NET comprobar que su clave interna es aceptada."""
    return {"message": "pong", "version": __version__}
