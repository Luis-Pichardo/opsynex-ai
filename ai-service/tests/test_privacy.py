"""Ningún dato personal llega a Claude."""

import pytest

from app.llm.privacy import PersonalDataError, ensure_no_personal_data, find_personal_data
from app.llm.prompts import recommendation_prompt, summary_prompt
from app.schemas.insights import RecommendationRequest, SummaryRequest
from tests.test_insights_api import COMPANY, PROBLEM


@pytest.mark.parametrize(
    ("text", "kind"),
    [
        ("Juan, cédula 001-1234567-8", "cédula"),
        ("Proveedor RNC 101-01234-5", "RNC"),
        ("Escribe a ana@empresa.com", "correo electrónico"),
        ("Llama al 809-555-1234", "teléfono"),
        ("EMPLEADO 007 cobró la regalía", "empleado de la nómina"),
    ],
)
def test_detects_personal_data(text: str, kind: str) -> None:
    assert kind in find_personal_data(text)
    with pytest.raises(PersonalDataError):
        ensure_no_personal_data(text)


@pytest.mark.parametrize(
    "text",
    [
        "Ingresos de RD$ 181.741.000 y margen 18,61 %",
        '{"revenue": 181741000.0, "employees": 15, "year": 2024}',
        "Monto 18174100000.5 y 12116066.67 por empleado",
    ],
)
def test_aggregates_are_allowed(text: str) -> None:
    assert find_personal_data(text) == []


def test_built_prompts_contain_only_aggregates() -> None:
    recommendation = recommendation_prompt(RecommendationRequest(company=COMPANY, problem=PROBLEM))
    summary = summary_prompt(SummaryRequest(company=COMPANY, kpis=PROBLEM["related_kpis"]))
    assert find_personal_data(recommendation) == []
    assert find_personal_data(summary) == []
