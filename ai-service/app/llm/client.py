"""Cliente de Claude con salida estructurada (Pydantic), manejo de errores y registro de uso.

- Salida estructurada con `client.beta.messages.parse(output_format=Modelo)`: la respuesta llega
  ya validada contra el esquema Pydantic.
- Se revisa `stop_reason` antes de leer el contenido (incluido `refusal`).
- Respaldo del servidor ante rechazos (`fallbacks="default"`), configurable.
- El system prompt es fijo y lleva `cache_control` (prompt caching); los datos variables van en
  el mensaje del usuario, al final.
- Errores del SDK traducidos a `LlmError` por tipo (límite de uso, 5xx, conexión, timeout).
- Se registran los tokens de cada llamada (entrada, salida y caché).
"""

from __future__ import annotations

import logging
from dataclasses import dataclass, field
from typing import Any, Protocol

import anthropic
from pydantic import BaseModel

from app.config import Settings, get_settings
from app.llm.privacy import PersonalDataError, ensure_no_personal_data

logger = logging.getLogger("opsynex.llm")

FALLBACK_BETA = "server-side-fallback-2026-07-01"


class LlmError(Exception):
    """Error de la capa de IA con el código y el estado HTTP que devuelve este servicio."""

    def __init__(self, code: str, message: str, status: int = 503) -> None:
        super().__init__(message)
        self.code = code
        self.message = message
        self.status = status


@dataclass(frozen=True)
class LlmUsage:
    """Tokens de una llamada (para registrar costos)."""

    input_tokens: int = 0
    output_tokens: int = 0
    cache_creation_input_tokens: int = 0
    cache_read_input_tokens: int = 0


@dataclass(frozen=True)
class LlmResult[T: BaseModel]:
    """Respuesta validada de Claude, con el modelo que respondió y el uso de tokens."""

    data: T
    model: str
    usage: LlmUsage = field(default_factory=LlmUsage)


class LlmClient(Protocol):
    """Lo que necesitan los endpoints: generar una salida estructurada a partir de un prompt."""

    async def generate[T: BaseModel](
        self, *, system: str, user: str, output: type[T], purpose: str, max_tokens: int = 16000
    ) -> LlmResult[T]: ...


class ClaudeClient:
    """Implementación con el SDK oficial `anthropic` (cliente asíncrono)."""

    def __init__(self, settings: Settings, sdk: Any | None = None) -> None:
        self._settings = settings
        self._sdk = sdk or anthropic.AsyncAnthropic(
            api_key=settings.anthropic_api_key,
            timeout=settings.llm_timeout_seconds,
            max_retries=settings.llm_max_retries,
        )

    @property
    def model(self) -> str:
        return self._settings.anthropic_model

    async def generate[T: BaseModel](
        self, *, system: str, user: str, output: type[T], purpose: str, max_tokens: int = 16000
    ) -> LlmResult[T]:
        try:
            ensure_no_personal_data(system)
            ensure_no_personal_data(user)
        except PersonalDataError as error:
            raise LlmError("llm_personal_data_blocked", str(error), status=422) from error

        kwargs: dict[str, Any] = {
            "model": self._settings.anthropic_model,
            "max_tokens": max_tokens,
            # Instrucciones fijas primero (cacheables); los datos de la empresa, al final.
            "system": [{"type": "text", "text": system, "cache_control": {"type": "ephemeral"}}],
            "messages": [{"role": "user", "content": user}],
            "output_format": output,
            "output_config": {"effort": self._settings.anthropic_effort},
        }
        if self._settings.anthropic_fallbacks.lower() != "none":
            kwargs["betas"] = [FALLBACK_BETA]
            kwargs["fallbacks"] = self._settings.anthropic_fallbacks

        try:
            response = await self._sdk.beta.messages.parse(**kwargs)
        except anthropic.AuthenticationError as error:
            raise LlmError(
                "llm_not_configured", "La clave de la API de Claude no es válida.", 503
            ) from error
        except anthropic.PermissionDeniedError as error:
            raise LlmError(
                "llm_not_configured", "La clave de Claude no tiene permiso para este modelo.", 503
            ) from error
        except anthropic.NotFoundError as error:
            raise LlmError(
                "llm_bad_model", f"El modelo «{self.model}» no existe o no está disponible.", 503
            ) from error
        except anthropic.RateLimitError as error:
            raise LlmError(
                "llm_rate_limited", "Se alcanzó el límite de uso de Claude; intenta más tarde.", 429
            ) from error
        except anthropic.BadRequestError as error:
            raise LlmError(
                "llm_bad_request", f"Claude rechazó la petición: {error.message}", 502
            ) from error
        except anthropic.APITimeoutError as error:
            raise LlmError("llm_timeout", "Claude no respondió a tiempo.", 504) from error
        except anthropic.APIStatusError as error:
            raise LlmError(
                "llm_upstream_error", f"Claude respondió {error.status_code}.", 502
            ) from error
        except anthropic.APIConnectionError as error:
            raise LlmError("llm_unavailable", "No se pudo conectar con Claude.", 503) from error

        usage = _usage(response)
        logger.info(
            "llm.call purpose=%s model=%s stop=%s input=%d output=%d cache_write=%d cache_read=%d",
            purpose,
            getattr(response, "model", self.model),
            response.stop_reason,
            usage.input_tokens,
            usage.output_tokens,
            usage.cache_creation_input_tokens,
            usage.cache_read_input_tokens,
        )

        if response.stop_reason == "refusal":
            details = getattr(response, "stop_details", None)
            category = getattr(details, "category", None) if details else None
            raise LlmError(
                "llm_refused",
                "Claude no generó la respuesta por sus políticas de seguridad"
                + (f" ({category})." if category else "."),
                422,
            )
        if response.stop_reason == "max_tokens":
            raise LlmError("llm_truncated", "La respuesta de Claude quedó incompleta.", 502)

        parsed = getattr(response, "parsed_output", None)
        if parsed is None:
            raise LlmError("llm_invalid_output", "Claude no devolvió el formato esperado.", 502)
        return LlmResult(data=parsed, model=getattr(response, "model", self.model), usage=usage)


def _usage(response: Any) -> LlmUsage:
    usage = getattr(response, "usage", None)
    if usage is None:
        return LlmUsage()
    return LlmUsage(
        input_tokens=getattr(usage, "input_tokens", 0) or 0,
        output_tokens=getattr(usage, "output_tokens", 0) or 0,
        cache_creation_input_tokens=getattr(usage, "cache_creation_input_tokens", 0) or 0,
        cache_read_input_tokens=getattr(usage, "cache_read_input_tokens", 0) or 0,
    )


def get_llm_client() -> LlmClient:
    """Dependencia de FastAPI: el cliente de Claude, o 503 si falta la clave."""
    settings = get_settings()
    if not settings.anthropic_api_key:
        raise LlmError(
            "llm_not_configured",
            "La IA generativa no está configurada (falta ANTHROPIC_API_KEY en el servicio de IA).",
            503,
        )
    return ClaudeClient(settings)
