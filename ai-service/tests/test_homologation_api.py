from fastapi.testclient import TestClient

from tests.conftest import TEST_API_KEY

HEADERS = {"X-Internal-Api-Key": TEST_API_KEY}


def test_suggest_requires_internal_key(client: TestClient) -> None:
    response = client.post("/homologation/suggest", json={"accounts": []})
    assert response.status_code == 401


def test_suggest_returns_one_suggestion_per_account(client: TestClient) -> None:
    payload = {
        "accounts": [
            {
                "id": "1",
                "label": "Energía Eléctrica (nota 13)",
                "sheet_type": "Notes",
                "section": "13. GASTOS",
            },
            {
                "id": "2",
                "label": "Total corriente",
                "normalized_label": "Total corriente",
                "sheet_type": "BalanceSheet",
                "section": "PASIVO CORRIENTE",
            },
            {"id": "3", "label": "Cuenta rara", "sheet_type": "IncomeStatement"},
        ],
        "known_mappings": [
            {
                "normalized_label": "Cuenta rara",
                "category": "overhead_cost",
                "sheet_type": "IncomeStatement",
            }
        ],
    }

    response = client.post("/homologation/suggest", json=payload, headers=HEADERS)

    assert response.status_code == 200
    suggestions = response.json()["suggestions"]
    assert [s["id"] for s in suggestions] == ["1", "2", "3"]
    assert suggestions[0]["category"] == "energy_cost"
    assert suggestions[1]["category"] == "current_liabilities"
    assert suggestions[2] == {
        "id": "3",
        "label": "Cuenta rara",
        "category": "overhead_cost",
        "confidence": 1.0,
        "method": "known",
        "alternatives": [],
    }
    assert all(0 <= s["confidence"] <= 1 for s in suggestions)


def test_suggest_validates_categories(client: TestClient) -> None:
    payload = {
        "accounts": [],
        "known_mappings": [{"normalized_label": "x", "category": "no_existe"}],
    }
    response = client.post("/homologation/suggest", json=payload, headers=HEADERS)
    assert response.status_code == 422
