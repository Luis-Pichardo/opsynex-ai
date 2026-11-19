"""Id de correlación entre el API .NET y el servicio de IA."""

import logging

import pytest

from app.correlation import HEADER, CorrelationIdFilter, correlation_id, sanitize


def test_echoes_a_valid_correlation_id(client) -> None:
    response = client.get("/health", headers={HEADER: "pedido-123.abc"})
    assert response.headers[HEADER] == "pedido-123.abc"


def test_creates_one_when_missing_or_unsafe(client) -> None:
    created = client.get("/health").headers[HEADER]
    assert len(created) == 32
    replaced = client.get("/health", headers={HEADER: "x" * 100}).headers[HEADER]
    assert replaced != "x" * 100


def test_unauthorized_responses_also_carry_it(client) -> None:
    response = client.post("/anomalies/detect", json={}, headers={HEADER: "abc"})
    assert response.status_code == 401
    assert response.headers[HEADER] == "abc"


@pytest.mark.parametrize(
    ("value", "expected"),
    [("abc-1_2.3", "abc-1_2.3"), ("con espacio", None), ("", None), (None, None), ("a" * 65, None)],
)
def test_sanitize(value: str | None, expected: str | None) -> None:
    assert sanitize(value) == expected


def test_log_records_include_the_id() -> None:
    record = logging.LogRecord("x", logging.INFO, __file__, 1, "msg", None, None)
    token = correlation_id.set("abc")
    try:
        assert CorrelationIdFilter().filter(record)
    finally:
        correlation_id.reset(token)
    assert record.correlation_id == "abc"  # type: ignore[attr-defined]
