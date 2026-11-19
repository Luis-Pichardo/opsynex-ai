"""Sugerencias de homologación contable (protegido por la clave interna)."""

from fastapi import APIRouter

from app.engines.homologation import AccountInput, KnownMapping, suggest
from app.schemas.homologation import (
    AlternativeOut,
    SuggestionOut,
    SuggestRequest,
    SuggestResponse,
)

router = APIRouter(prefix="/homologation", tags=["homologation"])


@router.post("/suggest", response_model=SuggestResponse)
async def suggest_mappings(request: SuggestRequest) -> SuggestResponse:
    """Sugiere la categoría Opsynex de cada cuenta. Los mapeos confirmados tienen prioridad."""
    accounts = [AccountInput(**a.model_dump()) for a in request.accounts]
    known = [KnownMapping(**k.model_dump()) for k in request.known_mappings]
    results = suggest(accounts, known)
    return SuggestResponse(
        suggestions=[
            SuggestionOut(
                id=s.id,
                label=s.label,
                category=s.category,
                confidence=s.confidence,
                method=s.method,
                alternatives=[
                    AlternativeOut(category=a.category, confidence=a.confidence)
                    for a in s.alternatives
                ],
            )
            for s in results
        ]
    )
