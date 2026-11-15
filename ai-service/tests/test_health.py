from fastapi.testclient import TestClient


def test_health_returns_200_without_api_key(client: TestClient) -> None:
    response = client.get("/health")

    assert response.status_code == 200
    body = response.json()
    assert body["status"] == "Healthy"
    assert body["service"] == "opsynex-ai-service"
    assert body["claudeConfigured"] is False
    assert body["model"]
