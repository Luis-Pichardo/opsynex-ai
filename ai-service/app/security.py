"""Autenticación interna: solo el API .NET puede llamar a este servicio."""

import secrets

from fastapi import Request
from fastapi.responses import JSONResponse
from starlette.middleware.base import BaseHTTPMiddleware, RequestResponseEndpoint
from starlette.responses import Response

from app.config import get_settings

API_KEY_HEADER = "X-Internal-Api-Key"
PUBLIC_PATHS = frozenset({"/health"})


def _problem(status: int, title: str, code: str, detail: str) -> JSONResponse:
    """Respuesta de error en formato ProblemDetails (RFC 7807), igual que el API .NET."""
    return JSONResponse(
        status_code=status,
        media_type="application/problem+json",
        content={
            "type": "about:blank",
            "title": title,
            "status": status,
            "detail": detail,
            "code": code,
        },
    )


class InternalApiKeyMiddleware(BaseHTTPMiddleware):
    """Exige la cabecera X-Internal-Api-Key en todas las rutas excepto /health."""

    async def dispatch(self, request: Request, call_next: RequestResponseEndpoint) -> Response:
        if request.url.path in PUBLIC_PATHS:
            return await call_next(request)

        expected = get_settings().internal_api_key
        if not expected:
            return _problem(
                503,
                "Servicio no configurado",
                "ai_service_not_configured",
                "Falta la variable AI_SERVICE_API_KEY en el servicio de IA.",
            )

        provided = request.headers.get(API_KEY_HEADER, "")
        if not secrets.compare_digest(provided.encode(), expected.encode()):
            return _problem(
                401,
                "No autorizado",
                "invalid_internal_api_key",
                f"Falta la cabecera {API_KEY_HEADER} o no es válida.",
            )

        return await call_next(request)
