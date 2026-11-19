"""Sugerencia de categoría para una cuenta contable (funciones puras).

Orden: mapeo ya confirmado por la empresa → reglas de contexto → coincidencia exacta →
palabras clave → coincidencia difusa (rapidfuzz). Si nada coincide, ``other`` con confianza
baja (la cuenta queda como dudosa).
"""

import re
from dataclasses import dataclass, field

from rapidfuzz import fuzz, process

from app.engines.homologation.categories import (
    ACCOUNT_CODE_GROUPS,
    ALL,
    BALANCE,
    CASH_FLOW,
    INCOME,
    Category,
)
from app.engines.homologation.dictionary import ENTRIES, Entry
from app.engines.homologation.normalize import normalize_label

C = Category

CONFIDENCE_KNOWN = 1.0
CONFIDENCE_RULE = 0.97
CONFIDENCE_EXACT = 0.97
FUZZY_THRESHOLD = 82.0
CONFIDENCE_NONE = 0.2
MAX_ALTERNATIVES = 3

# Secciones de notas que desglosan el balance: ahí solo caben categorías del balance.
_BALANCE_NOTE_SECTION = re.compile(
    r"(efectivo|caja|banco|cuentas por cobrar|propiedad|planta y equipo|otros activos|prestamo|"
    r"cuentas por pagar|acumulaciones|retenciones|inversiones|deuda|capital|patrimonio)"
)
_CURRENT_TOTAL = re.compile(r"^total (corriente|circulante)s?$")


@dataclass(frozen=True)
class AccountInput:
    id: str | None
    label: str
    normalized_label: str | None = None
    sheet_type: str | None = None
    section: str | None = None
    account_code: str | None = None


@dataclass(frozen=True)
class KnownMapping:
    normalized_label: str
    category: Category
    sheet_type: str | None = None
    section: str | None = None


@dataclass(frozen=True)
class Alternative:
    category: Category
    confidence: float


@dataclass(frozen=True)
class Suggestion:
    id: str | None
    label: str
    category: Category
    confidence: float
    method: str
    alternatives: list[Alternative] = field(default_factory=list)


def allowed_categories(account: AccountInput) -> frozenset[Category]:
    """Categorías posibles según el tipo de hoja, la sección y el código de cuenta."""
    sheet = account.sheet_type or ""
    if sheet == "BalanceSheet":
        allowed = BALANCE
    elif sheet in ("IncomeStatement", "CostStructure"):
        allowed = INCOME
    elif sheet == "CashFlow":
        allowed = CASH_FLOW
    elif sheet == "Notes" and _BALANCE_NOTE_SECTION.search(normalize_label(account.section)):
        allowed = BALANCE
    else:
        allowed = ALL

    code = (account.account_code or "").strip()
    if code[:1] in ACCOUNT_CODE_GROUPS:
        allowed = allowed & ACCOUNT_CODE_GROUPS[code[:1]] or ACCOUNT_CODE_GROUPS[code[:1]]
    return allowed | {C.OTHER}


def _entries_for(account: AccountInput, allowed: frozenset[Category]) -> list[Entry]:
    sheet = account.sheet_type or ""
    return [
        e
        for e in ENTRIES
        if e.category in allowed and (e.sheet_types is None or not sheet or sheet in e.sheet_types)
    ]


def _context_rule(text: str, account: AccountInput) -> Category | None:
    """Reglas que dependen de la sección: "Total corriente" de activos o de pasivos."""
    if _CURRENT_TOTAL.match(text):
        section = normalize_label(account.section)
        if "pasivo" in section:
            return C.CURRENT_LIABILITIES
        if "activo" in section:
            return C.CURRENT_ASSETS
    if not text:
        return C.OTHER
    return None


def _same_scope(known: KnownMapping, account: AccountInput) -> bool:
    if known.sheet_type and account.sheet_type and known.sheet_type != account.sheet_type:
        return False
    return not (
        known.section and normalize_label(known.section) != normalize_label(account.section)
    )


def suggest_one(account: AccountInput, known: list[KnownMapping] | None = None) -> Suggestion:
    text = normalize_label(account.normalized_label or account.label)
    allowed = allowed_categories(account)

    def result(
        category: Category,
        confidence: float,
        method: str,
        alternatives: list[Alternative] | None = None,
    ):
        return Suggestion(
            account.id, account.label, category, round(confidence, 2), method, alternatives or []
        )

    for mapping in known or []:
        if normalize_label(mapping.normalized_label) == text and _same_scope(mapping, account):
            return result(mapping.category, CONFIDENCE_KNOWN, "known")

    rule = _context_rule(text, account)
    if rule is not None:
        return result(rule, CONFIDENCE_RULE, "rule")

    entries = _entries_for(account, allowed)
    exact = [e for e in entries if text in e.terms]
    if exact:
        best = max(exact, key=lambda e: e.priority)
        return result(
            best.category, CONFIDENCE_EXACT, "exact", _alternatives(text, entries, best.category)
        )

    padded = f" {text} "
    keyword_hits = [(e, term) for e in entries for term in e.terms if f" {term} " in padded]
    if keyword_hits:
        entry, term = max(
            keyword_hits, key=lambda hit: (hit[0].priority, len(hit[1].split()), len(hit[1]))
        )
        coverage = len(term) / max(len(text), 1)
        confidence = min(0.95, 0.8 + 0.15 * coverage)
        return result(
            entry.category, confidence, "keyword", _alternatives(text, entries, entry.category)
        )

    candidates = {term: e.category for e in entries for term in e.terms}
    if candidates and text:
        match = process.extractOne(text, list(candidates), scorer=fuzz.WRatio)
        if match and match[1] >= FUZZY_THRESHOLD:
            category = candidates[match[0]]
            confidence = min(0.9, match[1] / 100 * 0.92)
            return result(category, confidence, "fuzzy", _alternatives(text, entries, category))

    return result(C.OTHER, CONFIDENCE_NONE, "none", _alternatives(text, entries, C.OTHER))


def _alternatives(text: str, entries: list[Entry], chosen: Category) -> list[Alternative]:
    """Las categorías más parecidas, distintas de la elegida."""
    if not text:
        return []
    best: dict[Category, float] = {}
    for entry in entries:
        if entry.category == chosen:
            continue
        for term in entry.terms:
            score = fuzz.WRatio(text, term) / 100
            if score > best.get(entry.category, 0):
                best[entry.category] = score
    ranked = sorted(best.items(), key=lambda item: item[1], reverse=True)[:MAX_ALTERNATIVES]
    return [
        Alternative(category, round(score * 0.9, 2)) for category, score in ranked if score >= 0.5
    ]


def suggest(
    accounts: list[AccountInput], known: list[KnownMapping] | None = None
) -> list[Suggestion]:
    """Una sugerencia por cuenta, en el mismo orden."""
    return [suggest_one(account, known) for account in accounts]
