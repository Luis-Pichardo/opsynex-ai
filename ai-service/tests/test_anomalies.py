"""Detección de anomalías con series sintéticas."""

import pytest

from app.engines.anomalies import detect, robust_zscores

MONTHS = [f"2024-{m:02d}" for m in range(1, 13)]


def series(values: list[float]) -> list[tuple[str, float]]:
    return list(zip(MONTHS[: len(values)], values, strict=True))


def test_flags_months_far_from_the_median() -> None:
    values = [13.0, 13.5, 4.0, 14.2, 13.8, 14.5, 32.0, 14.1, 13.9, 14.6, 15.0, 15.4]
    found = detect(series(values))
    assert [(a.period, a.direction, a.method) for a in found] == [
        ("2024-03", "low", "robust_zscore"),
        ("2024-07", "high", "robust_zscore"),
    ]
    assert found[1].expected == pytest.approx(14.15)
    assert found[1].score is not None and found[1].score > 3.5


def test_seasonal_peak_below_materiality_is_not_an_anomaly() -> None:
    # Serie como la del libro sintético: diciembre +22 % sobre la mediana (z ≈ 4,2).
    values = [13.4, 13.0, 15.0, 14.5, 15.4, 15.8, 15.6, 15.9, 15.2, 16.1, 16.7, 18.9]
    assert detect(series(values)) == []
    flagged = detect(series(values), min_deviation=0.2)
    assert [a.period for a in flagged] == ["2024-12"]


def test_threshold_is_configurable() -> None:
    values = [10.0, 10.5, 9.8, 10.2, 10.1, 13.0, 10.3, 9.9]
    assert detect(series(values), threshold=3.5) != []
    assert detect(series(values), threshold=50) == []


def test_short_series_uses_abrupt_changes() -> None:
    found = detect(series([100.0, 105.0, 40.0, 42.0]))
    assert [(a.period, a.direction, a.method) for a in found] == [
        ("2024-03", "low", "abrupt_change")
    ]
    assert found[0].expected == 105.0


def test_constant_series_falls_back_to_abrupt_changes() -> None:
    values = [10.0, 10.0, 10.0, 10.0, 10.0, 10.0, 10.0, 30.0]
    assert robust_zscores(values) is None
    found = detect(series(values))
    assert [(a.period, a.method) for a in found] == [("2024-08", "abrupt_change")]


def test_too_short_or_zero_values() -> None:
    assert detect(series([5.0])) == []
    assert detect(series([0.0, 50.0, 51.0])) == []  # sin base para comparar el primer salto


def test_endpoint_explains_each_anomaly(client, auth_headers) -> None:
    values = [13.0, 13.5, 4.0, 14.2, 13.8, 14.5, 32.0, 14.1, 13.9, 14.6, 15.0, 15.4]
    payload = {
        "currency": "DOP",
        "series": [
            {
                "name": "MonthlyRevenue",
                "label": "las ventas",
                "unit": "Money",
                "points": [{"period": p, "value": v * 1_000_000} for p, v in series(values)],
            }
        ],
    }
    response = client.post("/anomalies/detect", json=payload, headers=auth_headers)
    assert response.status_code == 200
    anomalies = response.json()["anomalies"]
    assert [a["period"] for a in anomalies] == ["2024-03", "2024-07"]
    assert anomalies[1]["explanation"].startswith(
        "El valor de las ventas en 2024-07 (RD$ 32.000.000)"
    )
    assert "mediana RD$ 14.150.000" in anomalies[1]["explanation"]


def test_endpoint_detects_daily_industrial_series(client, auth_headers) -> None:
    # 60 días hábiles de la planta (módulo industrial): un día con una avería mayor.
    days = [f"2024-{10 + d // 30:02d}-{d % 30 + 1:02d}" for d in range(60)]
    downtime = [300.0 + (d % 5) * 12 for d in range(60)]
    oee = [0.72 + (d % 4) * 0.01 for d in range(60)]
    downtime[40] = 1150.0
    oee[40] = 0.31
    payload = {
        "currency": "DOP",
        "series": [
            {
                "name": "DailyDowntime2024",
                "label": "los minutos de parada diarios de la planta",
                "unit": "Number",
                "points": [{"period": p, "value": v} for p, v in zip(days, downtime, strict=True)],
            },
            {
                "name": "DailyOee2024",
                "label": "el OEE diario de la planta",
                "unit": "Ratio",
                "points": [{"period": p, "value": v} for p, v in zip(days, oee, strict=True)],
            },
        ],
    }
    response = client.post("/anomalies/detect", json=payload, headers=auth_headers)
    assert response.status_code == 200
    anomalies = response.json()["anomalies"]
    assert [(a["series"], a["period"], a["direction"]) for a in anomalies] == [
        ("DailyDowntime2024", "2024-11-11", "high"),
        ("DailyOee2024", "2024-11-11", "low"),
    ]
    assert anomalies[0]["explanation"].startswith(
        "El valor de los minutos de parada diarios de la planta en 2024-11-11 (1.150)"
    )
    assert "(31,0 %)" in anomalies[1]["explanation"]
