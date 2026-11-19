"""Motor de homologación contable: diccionario + palabras clave + coincidencia difusa."""

from app.engines.homologation.matcher import AccountInput, KnownMapping, Suggestion, suggest

__all__ = ["AccountInput", "KnownMapping", "Suggestion", "suggest"]
