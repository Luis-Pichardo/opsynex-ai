"""Normalización de etiquetas contables para comparar."""

import re
import unicodedata

_NOTE_REF = re.compile(r"\(\s*(nota\s*)?\d+(\.\d+)?\s*\)", re.IGNORECASE)
_LOOSE_NOTE = re.compile(r"\bnotas?\s*\d+(\.\d+)?\b", re.IGNORECASE)
_PREFIX = re.compile(r"^\s*(menos|m[aá]s)\s*:\s*", re.IGNORECASE)
_NON_WORD = re.compile(r"[^0-9a-z%]+")
_DIGITS = re.compile(r"\b\d+\b")
_SPACES = re.compile(r"\s+")


def strip_accents(text: str) -> str:
    """Quita acentos y diacríticos ("Energía" → "Energia")."""
    decomposed = unicodedata.normalize("NFD", text)
    return "".join(ch for ch in decomposed if unicodedata.category(ch) != "Mn")


def normalize_label(text: str | None) -> str:
    """Minúsculas, sin acentos, sin referencias a notas, sin prefijos "Menos:"/"más:",
    sin códigos numéricos, "&" → "y" y con espacios simples."""
    if not text:
        return ""
    value = _NOTE_REF.sub(" ", text)
    value = _LOOSE_NOTE.sub(" ", value)
    value = _PREFIX.sub("", value)
    value = strip_accents(value).lower().replace("&", " y ")
    value = _NON_WORD.sub(" ", value)
    value = _DIGITS.sub(" ", value)
    return _SPACES.sub(" ", value).strip()
