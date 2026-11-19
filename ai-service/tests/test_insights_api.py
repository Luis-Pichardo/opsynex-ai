"""Endpoints de recomendaciones y resumen ejecutivo con el cliente de Claude simulado."""

import json

import pytest

from app.llm.client import LlmError, get_llm_client
from app.schemas.insights import ActionDraft, RecommendationDraft, SummaryDraft
from tests.fakes import FakeLlm

COMPANY = {
    "industry": "Manufacturing",
    "country": "República Dominicana",
    "currency": "DOP",
    "year": 2024,
}

PROBLEM = {
    "code": "EBITDA_DECLINE",
    "title": "Deterioro de rentabilidad",
    "description": "El margen EBITDA bajó de 19,75 % a 18,61 % (-1,14 pp).",
    "severity": "Medium",
    "category": "Profitability",
    "impact_category": "MarginLoss",
    "impact_value": 2071847.4,
    "affected_margin_pp": 1.14,
    "evidence": {"previous": 0.1975, "current": 0.1861, "changePp": -1.14},
    "base_recommendation": "Revisa qué costos crecieron más que los ingresos.",
    "related_kpis": [
        {
            "code": "EbitdaMargin",
            "name": "Margen EBITDA",
            "unit": "Ratio",
            "value": 0.1861,
            "previous": 0.1975,
        }
    ],
}

RECOMMENDATION = RecommendationDraft(
    summary="El margen EBITDA cayó 1,14 pp.",
    root_causes=["Costo de ventas creció más que los ingresos", "Aumento de la nómina"],
    actions=[
        ActionDraft(
            title="Renegocia con los 3 principales proveedores",
            description="Busca descuentos por volumen.",
            expected_impact="Recuperar 0,5 pp de margen.",
            effort="medium",
            horizon="short",
        )
    ],
)


@pytest.fixture
def fake(client) -> FakeLlm:
    llm = FakeLlm(
        responses={
            RecommendationDraft: RECOMMENDATION,
            SummaryDraft: SummaryDraft(
                paragraphs=[" Uno. ", "Dos.", "Tres.", "Cuatro.", "Cinco.", "Seis."],
                priorities=["A", "B", "C", "D"],
            ),
        }
    )
    client.app.dependency_overrides[get_llm_client] = lambda: llm
    yield llm
    client.app.dependency_overrides.clear()


def test_recommendation_returns_causes_and_actions(client, auth_headers, fake: FakeLlm) -> None:
    response = client.post(
        "/recommendations/generate",
        json={"company": COMPANY, "problem": PROBLEM},
        headers=auth_headers,
    )
    assert response.status_code == 200
    body = response.json()
    assert body["summary"] == "El margen EBITDA cayó 1,14 pp."
    assert body["root_causes"][0].startswith("Costo de ventas")
    assert body["actions"][0] | {} == {
        "title": "Renegocia con los 3 principales proveedores",
        "description": "Busca descuentos por volumen.",
        "expected_impact": "Recuperar 0,5 pp de margen.",
        "effort": "medium",
        "horizon": "short",
    }
    assert body["model"] == "claude-test"

    call = fake.calls[0]
    assert call["purpose"] == "recommendation:EBITDA_DECLINE"
    # Contexto del país y terminología local en el system prompt (fijo y cacheable).
    assert "República Dominicana" in call["system"] and "ITBIS" in call["system"]
    assert "Regalía Pascual" in call["system"] and "RD$" in call["system"]
    # Los datos van en el mensaje del usuario como JSON ordenado.
    context = call["user"].split("<contexto>")[1].split("</contexto>")[0]
    assert json.loads(context)["problem"]["code"] == "EBITDA_DECLINE"


def test_summary_keeps_up_to_5_paragraphs_and_3_priorities(
    client, auth_headers, fake: FakeLlm
) -> None:
    payload = {
        "company": COMPANY,
        "score": {
            "total": 78.4,
            "interpretation": "Oportunidades de mejora",
            "components": {"Rentabilidad": 77},
        },
        "previous_score": 80.7,
        "kpis": PROBLEM["related_kpis"],
        "problems": [
            {
                "title": "Deterioro de rentabilidad",
                "severity": "Low",
                "impact_value": 2071847.4,
                "description": "…",
            }
        ],
        "highlights": ["Menor endeudamiento"],
        "total_estimated_loss": 4397941.56,
        "revenue": 181741000,
    }
    response = client.post("/executive-summary", json=payload, headers=auth_headers)
    assert response.status_code == 200
    body = response.json()
    assert body["paragraphs"] == ["Uno.", "Dos.", "Tres.", "Cuatro.", "Cinco."]
    assert body["priorities"] == ["A", "B", "C"]
    assert fake.calls[0]["purpose"] == "executive_summary"


def test_without_anthropic_key_returns_503_problem(client, auth_headers) -> None:
    response = client.post(
        "/recommendations/generate",
        json={"company": COMPANY, "problem": PROBLEM},
        headers=auth_headers,
    )
    assert response.status_code == 503
    assert response.headers["content-type"].startswith("application/problem+json")
    assert response.json()["code"] == "llm_not_configured"


def test_llm_errors_become_problem_details(client, auth_headers, fake: FakeLlm) -> None:
    fake.error = LlmError("llm_rate_limited", "Límite alcanzado.", 429)
    response = client.post(
        "/recommendations/generate",
        json={"company": COMPANY, "problem": PROBLEM},
        headers=auth_headers,
    )
    assert response.status_code == 429
    assert response.json()["code"] == "llm_rate_limited"


def test_requires_internal_api_key(client, fake: FakeLlm) -> None:
    response = client.post(
        "/recommendations/generate", json={"company": COMPANY, "problem": PROBLEM}
    )
    assert response.status_code == 401
    assert fake.calls == []


def test_rejects_invalid_payloads(client, auth_headers, fake: FakeLlm) -> None:
    bad = {**PROBLEM, "severity": "Critical"}
    response = client.post(
        "/recommendations/generate", json={"company": COMPANY, "problem": bad}, headers=auth_headers
    )
    assert response.status_code == 422
    assert fake.calls == []
