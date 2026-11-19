"""Defensa en profundidad: ningún dato personal sale hacia Claude.

El API .NET solo envía agregados y KPIs, pero los archivos de los clientes traen nóminas con
nombres, cédulas y salarios (FORMATO_EXCEL_ENTRADA.md). Antes de cada llamada a Claude se
revisa el texto y, si aparece algo con forma de dato personal, la llamada se cancela.
"""

import re

# Cédula dominicana (000-0000000-0) y RNC (000-00000-0) con guiones. Sin guiones no se busca:
# un monto grande sin separadores se confundiría con una cédula.
_PATTERNS: dict[str, re.Pattern[str]] = {
    "cédula": re.compile(r"\b\d{3}-\d{7}-\d\b"),
    "RNC": re.compile(r"\b\d{3}-\d{5}-\d\b"),
    "correo electrónico": re.compile(r"\b[\w.+-]+@[\w-]+\.[\w.-]+\b"),
    "teléfono": re.compile(r"\b(?:\+?1[\s-]?)?\(?8[024]9\)?[\s-]?\d{3}[\s-]?\d{4}\b"),
    # Filas de nómina de los libros (EMPLEADO 001…).
    "empleado de la nómina": re.compile(r"\bEMPLEADO\s+\d{2,}\b", re.IGNORECASE),
}


class PersonalDataError(ValueError):
    """El texto contiene algo con forma de dato personal: no se envía a Claude."""


def find_personal_data(text: str) -> list[str]:
    """Tipos de dato personal encontrados en el texto (vacío si no hay)."""
    return [kind for kind, pattern in _PATTERNS.items() if pattern.search(text)]


def ensure_no_personal_data(text: str) -> None:
    """Lanza `PersonalDataError` si el texto parece contener datos personales."""
    found = find_personal_data(text)
    if found:
        raise PersonalDataError(
            "La petición contiene datos que parecen personales ("
            + ", ".join(found)
            + "); no se envía a Claude."
        )
