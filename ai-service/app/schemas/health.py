"""Esquemas del endpoint de salud."""

from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel


class HealthResponse(BaseModel):
    """Estado del servicio de IA."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    status: str
    service: str
    version: str
    claude_configured: bool
    model: str
