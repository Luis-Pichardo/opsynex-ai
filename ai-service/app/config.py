"""Configuración del servicio, leída de variables de entorno (o de un archivo .env)."""

from functools import lru_cache

from pydantic import Field, field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Variables de entorno del servicio de IA."""

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    # Clave compartida con el API .NET (cabecera X-Internal-Api-Key).
    internal_api_key: str = Field(default="", alias="AI_SERVICE_API_KEY")
    anthropic_api_key: str = Field(default="", alias="ANTHROPIC_API_KEY")
    anthropic_model: str = Field(default="claude-opus-5-5", alias="ANTHROPIC_MODEL")
    # Esfuerzo de razonamiento de Claude (low, medium, high, xhigh, max). En Opus 5.5 el
    # predeterminado es "medium"; se fija explícito para que no cambie con el modelo.
    anthropic_effort: str = Field(default="medium", alias="ANTHROPIC_EFFORT")
    # Respaldo del servidor si el modelo rechaza la petición: "default" (recomendado) o "none".
    # Los modelos sin fallbacks del lado del servidor (p. ej. Haiku) necesitan "none".
    anthropic_fallbacks: str = Field(default="default", alias="ANTHROPIC_FALLBACKS")
    # Tiempo máximo de una llamada a Claude (segundos) y reintentos del SDK.
    llm_timeout_seconds: float = Field(default=90.0, alias="LLM_TIMEOUT_SECONDS")
    llm_max_retries: int = Field(default=2, alias="LLM_MAX_RETRIES")
    log_level: str = Field(default="INFO", alias="LOG_LEVEL")

    @field_validator("anthropic_api_key")
    @classmethod
    def _placeholder_means_not_configured(cls, value: str) -> str:
        # Key Vault no admite secretos vacíos: "disabled" (o "none") significa sin clave de Claude.
        value = value.strip()
        return "" if value.lower() in {"disabled", "none", "-"} else value


@lru_cache
def get_settings() -> Settings:
    """Devuelve la configuración (una sola instancia por proceso)."""
    return Settings()
