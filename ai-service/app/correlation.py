"""Id de correlación: el mismo X-Correlation-Id del API .NET en los logs de este servicio."""

from __future__ import annotations

import logging
import re
import uuid
from contextvars import ContextVar

from fastapi import Request
from starlette.middleware.base import BaseHTTPMiddleware, RequestResponseEndpoint
from starlette.responses import Response

HEADER = "X-Correlation-Id"
_SAFE = re.compile(r"^[A-Za-z0-9._-]{1,64}$")
correlation_id: ContextVar[str] = ContextVar("correlation_id", default="-")


def sanitize(value: str | None) -> str | None:
    """Acepta un id recibido solo si es corto y seguro para logs y cabeceras."""
    return value if value and _SAFE.match(value) else None


class CorrelationIdMiddleware(BaseHTTPMiddleware):
    """Toma el id de la petición (o crea uno), lo pone en los logs y lo devuelve en la respuesta."""

    async def dispatch(self, request: Request, call_next: RequestResponseEndpoint) -> Response:
        value = sanitize(request.headers.get(HEADER)) or uuid.uuid4().hex
        token = correlation_id.set(value)
        try:
            response = await call_next(request)
        finally:
            correlation_id.reset(token)
        response.headers[HEADER] = value
        return response


class CorrelationIdFilter(logging.Filter):
    """Agrega `correlation_id` a cada registro de log."""

    def filter(self, record: logging.LogRecord) -> bool:
        record.correlation_id = correlation_id.get()
        return True
