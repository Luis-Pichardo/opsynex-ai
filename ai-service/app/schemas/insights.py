"""Esquemas de recomendaciones, resumen ejecutivo y anomalías.

Las entradas solo traen agregados y KPIs (nunca nombres, cédulas ni salarios individuales).
Los modelos `*Draft` son la salida estructurada que se le pide a Claude.
"""

from typing import Literal

from pydantic import BaseModel, Field

Number = float | int | None


class CompanyContext(BaseModel):
    """Contexto de la empresa (sin su nombre: no hace falta para analizar)."""

    industry: str = Field(description="Manufacturing o Services")
    sector: str | None = None
    country: str = "República Dominicana"
    currency: str = "DOP"
    year: int


class KpiIn(BaseModel):
    code: str
    name: str
    unit: Literal["Ratio", "Times", "Money", "Days"]
    value: Number = None
    previous: Number = None


class ProblemIn(BaseModel):
    code: str
    title: str
    description: str
    severity: Literal["High", "Medium", "Low"]
    category: str
    impact_category: str
    impact_value: Number = None
    affected_margin_pp: Number = None
    evidence: dict[str, float | int | str | None] = Field(default_factory=dict)
    base_recommendation: str
    related_kpis: list[KpiIn] = Field(default_factory=list)


class RecommendationRequest(BaseModel):
    company: CompanyContext
    problem: ProblemIn


# --- Salida estructurada de Claude ---


class ActionDraft(BaseModel):
    title: str = Field(description="Acción concreta, en imperativo, en una línea")
    description: str = Field(description="Qué hacer y cómo, en 1 a 3 frases")
    expected_impact: str = Field(description="Efecto esperado, con cifras de los datos si aplica")
    effort: Literal["low", "medium", "high"]
    horizon: Literal["short", "medium", "long"] = Field(
        description="short: menos de 3 meses; medium: 3 a 12 meses; long: más de 12 meses"
    )


class RecommendationDraft(BaseModel):
    summary: str = Field(description="Diagnóstico en 2 o 3 frases")
    root_causes: list[str] = Field(description="2 a 4 causas raíz probables, de más a menos")
    actions: list[ActionDraft] = Field(description="3 a 5 acciones, de mayor a menor prioridad")


class RecommendationResponse(RecommendationDraft):
    model: str


class ScoreIn(BaseModel):
    total: float
    interpretation: str
    components: dict[str, float | None] = Field(default_factory=dict)


class ProblemBrief(BaseModel):
    title: str
    severity: Literal["High", "Medium", "Low"]
    impact_value: Number = None
    description: str


class SummaryRequest(BaseModel):
    company: CompanyContext
    score: ScoreIn | None = None
    previous_score: float | None = None
    kpis: list[KpiIn] = Field(default_factory=list)
    problems: list[ProblemBrief] = Field(default_factory=list)
    highlights: list[str] = Field(default_factory=list)
    total_estimated_loss: Number = None
    revenue: Number = None


class SummaryDraft(BaseModel):
    paragraphs: list[str] = Field(description="3 a 5 párrafos del resumen ejecutivo")
    priorities: list[str] = Field(description="Exactamente 3 prioridades, en orden")


class SummaryResponse(SummaryDraft):
    model: str


# --- Anomalías (sin LLM) ---


class SeriesPoint(BaseModel):
    period: str = Field(description="Período, p. ej. 2024-07")
    value: float


class SeriesIn(BaseModel):
    name: str = Field(description="Código de la serie, p. ej. MonthlyRevenue")
    label: str = Field(description="Nombre para las explicaciones, p. ej. «Ventas mensuales»")
    unit: Literal["Money", "Ratio", "Number"] = "Money"
    points: list[SeriesPoint]


class AnomalyRequest(BaseModel):
    series: list[SeriesIn]
    currency: str = "DOP"
    threshold: float = Field(default=3.5, gt=0, description="Umbral del z-score robusto")
    min_deviation: float = Field(
        default=0.25, ge=0, description="Distancia mínima a la mediana (proporción) para reportar"
    )


class AnomalyOut(BaseModel):
    series: str
    period: str
    value: float
    expected: float
    score: float | None
    direction: Literal["high", "low"]
    method: Literal["robust_zscore", "abrupt_change"]
    explanation: str


class AnomalyResponse(BaseModel):
    anomalies: list[AnomalyOut]
