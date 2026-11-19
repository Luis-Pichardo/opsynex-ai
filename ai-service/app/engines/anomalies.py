"""Detección de anomalías en series (sin LLM).

- Con 6 o más puntos: z-score robusto con la mediana y la MAD
  (z = 0,6745 · (x - mediana) / MAD). Un punto es atípico si |z| supera el umbral (3,5) **y**
  se aleja al menos un 25 % de la mediana (materialidad): en una serie estable la MAD es tan
  pequeña que un pico estacional moderado (p. ej. diciembre +20 %) daría un z alto sin ser raro.
- Con menos puntos, o si la MAD es 0: variaciones bruscas (más de ±50 % frente al punto anterior).

La serie útil con la estructura de los clientes es la de ingresos mensuales (12 puntos).
"""

from __future__ import annotations

from dataclasses import dataclass
from itertools import pairwise
from statistics import median

MIN_POINTS_FOR_ZSCORE = 6
ABRUPT_CHANGE = 0.5
MIN_DEVIATION = 0.25
MAD_SCALE = 0.6745


@dataclass(frozen=True)
class Anomaly:
    period: str
    value: float
    expected: float
    score: float | None
    direction: str
    method: str


def robust_zscores(values: list[float]) -> tuple[float, float, list[float]] | None:
    """Mediana, MAD y z-score robusto de cada valor; `None` si la MAD es 0."""
    center = median(values)
    mad = median(abs(v - center) for v in values)
    if mad == 0:
        return None
    return center, mad, [MAD_SCALE * (v - center) / mad for v in values]


def detect(
    points: list[tuple[str, float]], threshold: float = 3.5, min_deviation: float = MIN_DEVIATION
) -> list[Anomaly]:
    """Anomalías de una serie ordenada por período."""
    if len(points) < 2:
        return []
    values = [v for _, v in points]
    if len(points) >= MIN_POINTS_FOR_ZSCORE:
        stats = robust_zscores(values)
        if stats is not None:
            center, _, scores = stats
            return [
                Anomaly(
                    period, value, center, round(z, 2), "high" if z > 0 else "low", "robust_zscore"
                )
                for (period, value), z in zip(points, scores, strict=True)
                if abs(z) > threshold and _material(value, center, min_deviation)
            ]
    return _abrupt_changes(points)


def _material(value: float, center: float, min_deviation: float) -> bool:
    """El valor se aleja de la mediana al menos `min_deviation` (proporción)."""
    if center == 0:
        return value != 0
    return abs(value - center) / abs(center) >= min_deviation


def _abrupt_changes(points: list[tuple[str, float]]) -> list[Anomaly]:
    found: list[Anomaly] = []
    for (_, previous), (period, value) in pairwise(points):
        if previous == 0:
            continue
        change = (value - previous) / abs(previous)
        if abs(change) > ABRUPT_CHANGE:
            direction = "high" if change > 0 else "low"
            found.append(Anomaly(period, value, previous, None, direction, "abrupt_change"))
    return found
