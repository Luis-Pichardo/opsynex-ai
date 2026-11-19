"""Recomendaciones y resumen ejecutivo con Claude, y detección de anomalías (sin LLM)."""

from typing import Annotated

from fastapi import APIRouter, Depends

from app.engines.anomalies import Anomaly, detect
from app.llm.client import LlmClient, get_llm_client
from app.llm.prompts import SYSTEM_PROMPT, recommendation_prompt, summary_prompt
from app.schemas.insights import (
    AnomalyOut,
    AnomalyRequest,
    AnomalyResponse,
    RecommendationDraft,
    RecommendationRequest,
    RecommendationResponse,
    SummaryDraft,
    SummaryRequest,
    SummaryResponse,
)

router = APIRouter(tags=["insights"])

Llm = Annotated[LlmClient, Depends(get_llm_client)]


@router.post("/recommendations/generate", response_model=RecommendationResponse)
async def generate_recommendation(
    request: RecommendationRequest, llm: Llm
) -> RecommendationResponse:
    """Recomendación de IA para un problema: diagnóstico, causas raíz y acciones."""
    result = await llm.generate(
        system=SYSTEM_PROMPT,
        user=recommendation_prompt(request),
        output=RecommendationDraft,
        purpose=f"recommendation:{request.problem.code}",
    )
    return RecommendationResponse(**result.data.model_dump(), model=result.model)


@router.post("/executive-summary", response_model=SummaryResponse)
async def executive_summary(request: SummaryRequest, llm: Llm) -> SummaryResponse:
    """Resumen ejecutivo del año (3 a 5 párrafos) y 3 prioridades."""
    result = await llm.generate(
        system=SYSTEM_PROMPT,
        user=summary_prompt(request),
        output=SummaryDraft,
        purpose="executive_summary",
    )
    draft = result.data
    paragraphs = [p.strip() for p in draft.paragraphs if p.strip()][:5]
    priorities = [p.strip() for p in draft.priorities if p.strip()][:3]
    return SummaryResponse(paragraphs=paragraphs, priorities=priorities, model=result.model)


@router.post("/anomalies/detect", response_model=AnomalyResponse)
async def detect_anomalies(request: AnomalyRequest) -> AnomalyResponse:
    """Valores atípicos en series de KPIs (z-score robusto con MAD o variaciones bruscas)."""
    found: list[AnomalyOut] = []
    for series in request.series:
        points = [(p.period, p.value) for p in series.points]
        for anomaly in detect(points, request.threshold, request.min_deviation):
            found.append(
                AnomalyOut(
                    series=series.name,
                    period=anomaly.period,
                    value=anomaly.value,
                    expected=anomaly.expected,
                    score=anomaly.score,
                    direction="high" if anomaly.direction == "high" else "low",
                    method=(
                        "robust_zscore" if anomaly.method == "robust_zscore" else "abrupt_change"
                    ),
                    explanation=_explain(series.label, series.unit, request.currency, anomaly),
                )
            )
    return AnomalyResponse(anomalies=found)


def _amount(value: float, unit: str, currency: str) -> str:
    if unit == "Ratio":
        return f"{value * 100:.1f} %".replace(".", ",")
    text = f"{value:,.0f}".replace(",", ".")
    symbol = {"DOP": "RD$", "USD": "US$", "EUR": "€"}.get(currency.upper(), currency)
    return f"{symbol} {text}" if unit == "Money" else text


def _explain(label: str, unit: str, currency: str, anomaly: Anomaly) -> str:
    value = _amount(anomaly.value, unit, currency)
    expected = _amount(anomaly.expected, unit, currency)
    if anomaly.method == "robust_zscore":
        where = "por encima" if anomaly.direction == "high" else "por debajo"
        return (
            f"{label} de {anomaly.period} ({value}) está muy {where} de lo habitual "
            f"(mediana {expected}). Revisa si se debe a estacionalidad, a un registro contable "
            "atípico o a un error de carga."
        )
    change = (anomaly.value - anomaly.expected) / abs(anomaly.expected) * 100
    sign = "+" if change > 0 else "\u2212"  # signo menos tipográfico
    return (
        f"{label} cambió {sign}{abs(change):.0f} % en {anomaly.period} frente al período "
        f"anterior ({expected} → {value}). Verifica que el dato sea correcto."
    )
