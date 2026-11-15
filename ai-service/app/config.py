"""Configuración del servicio, leída de variables de entorno (o de un archivo .env)."""

from functools import lru_cache

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Variables de entorno del servicio de IA."""

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    # Clave compartida con el API .NET (cabecera X-Internal-Api-Key).
    internal_api_key: str = Field(default="", alias="AI_SERVICE_API_KEY")
    anthropic_api_key: str = Field(default="", alias="ANTHROPIC_API_KEY")
    anthropic_model: str = Field(default="claude-opus-5-5", alias="ANTHROPIC_MODEL")
    log_level: str = Field(default="INFO", alias="LOG_LEVEL")


@lru_cache
def get_settings() -> Settings:
    """Devuelve la configuración (una sola instancia por proceso)."""
    return Settings()
