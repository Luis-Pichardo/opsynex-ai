import pytest
from fastapi.testclient import TestClient

from app.config import get_settings
from app.main import create_app
from tests.conftest import TEST_API_KEY


def test_protected_route_without_key_returns_401(client: TestClient) -> None:
    response = client.get("/v1/ping")

    assert response.status_code == 401
    assert response.headers["content-type"].startswith("application/problem+json")
    assert response.json()["code"] == "invalid_internal_api_key"


def test_protected_route_with_wrong_key_returns_401(client: TestClient) -> None:
    response = client.get("/v1/ping", headers={"X-Internal-Api-Key": "wrong"})

    assert response.status_code == 401


def test_protected_route_with_valid_key_returns_200(client: TestClient) -> None:
    response = client.get("/v1/ping", headers={"X-Internal-Api-Key": TEST_API_KEY})

    assert response.status_code == 200
    assert response.json()["message"] == "pong"


def test_docs_are_protected(client: TestClient) -> None:
    assert client.get("/docs").status_code == 401
    assert (
        client.get("/openapi.json", headers={"X-Internal-Api-Key": TEST_API_KEY}).status_code == 200
    )


def test_missing_server_key_returns_503(monkeypatch: pytest.MonkeyPatch) -> None:
    # Una variable vacía también tiene prioridad sobre un posible archivo .env local.
    monkeypatch.setenv("AI_SERVICE_API_KEY", "")
    get_settings.cache_clear()
    try:
        client = TestClient(create_app())
        response = client.get("/v1/ping", headers={"X-Internal-Api-Key": "anything"})
        assert response.status_code == 503
        assert response.json()["code"] == "ai_service_not_configured"
    finally:
        get_settings.cache_clear()
