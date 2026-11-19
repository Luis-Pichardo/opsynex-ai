from fastapi.testclient import TestClient


def test_health_returns_200_without_api_key(client: TestClient) -> None:
    response = client.get("/health")

    assert response.status_code == 200
    body = response.json()
    assert body["status"] == "Healthy"
    assert body["service"] == "opsynex-ai-service"
    assert body["claudeConfigured"] is False
    assert body["model"]


def test_placeholder_key_means_claude_is_not_configured(monkeypatch) -> None:
    from app.config import Settings

    for value in ("disabled", " NONE ", "-"):
        monkeypatch.setenv("ANTHROPIC_API_KEY", value)
        assert Settings().anthropic_api_key == ""
    monkeypatch.setenv("ANTHROPIC_API_KEY", " sk-ant-prueba ")
    assert Settings().anthropic_api_key == "sk-ant-prueba"
