#!/usr/bin/env python3
"""Genera los datos industriales sintéticos de ``backend/sample-data/industrial/``.

Una planta ficticia con 3 máquinas en 2 líneas y 3 meses de producción diaria (oct–dic 2024,
lunes a sábado). Todos los valores son inventados y reproducibles (semilla fija):

- ``MAQ-01`` (LINEA-1): máquina sana (OEE ≈ 89 %).
- ``MAQ-02`` (LINEA-1): scrap alto (≈ 8 %) y costo por unidad ≈ 18 % sobre el estándar.
- ``MAQ-03`` (LINEA-2): paradas altas (≈ 24 % del tiempo) y ciclo lento → OEE ≈ 57 %.
- 14/11/2024: avería mayor en MAQ-02 y MAQ-03 (anomalía en las series diarias de OEE y paradas).

Además escribe ``expected_industrial.json`` con los KPIs y el impacto esperados, calculados aquí
de forma independiente al motor de .NET, para las pruebas de integración.

Requisitos: openpyxl. Uso: ``python3 backend/tests/TestData/generate_industrial_samples.py``.
"""

from __future__ import annotations

import csv
import json
import random
from dataclasses import dataclass
from datetime import date, timedelta
from pathlib import Path

from openpyxl import Workbook
from openpyxl.styles import Font

OUT = Path(__file__).resolve().parents[2] / "sample-data" / "industrial"
START, END = date(2024, 10, 1), date(2024, 12, 31)
HOLIDAYS = {date(2024, 12, 25)}
PLANNED = 960  # dos turnos de 8 horas
OUTAGE_DAY = date(2024, 11, 14)

# Umbrales por defecto de las reglas (appsettings → Industrial).
LOW_OEE, HIGH_SCRAP, HIGH_DOWNTIME, COST_TOLERANCE = 0.60, 0.05, 0.15, 0.10


@dataclass(frozen=True)
class Machine:
    id: str
    line: str
    process: str
    std_cycle: float  # segundos por unidad
    std_cost: float  # RD$ por unidad
    cost_per_minute: float  # RD$
    downtime: tuple[int, int]  # minutos por día (mín, máx)
    cycle: tuple[float, float]  # ciclo real (s)
    scrap: tuple[float, float]  # proporción
    target_cpu: float  # costo real por unidad (RD$)


MACHINES = [
    Machine("MAQ-01", "LINEA-1", "Inyección", 30.0, 32.50, 57.0, (25, 55), (31.0, 32.0), (0.015, 0.025), 31.80),
    Machine("MAQ-02", "LINEA-1", "Inyección", 26.0, 30.00, 66.0, (50, 90), (27.0, 28.0), (0.07, 0.09), 35.40),
    Machine("MAQ-03", "LINEA-2", "Ensamble", 36.0, 41.00, 42.0, (190, 270), (45.0, 47.0), (0.03, 0.04), 43.50),
]
OUTAGE = {"MAQ-02": 400, "MAQ-03": 900}
COST_SPLIT = {"labor": 0.22, "material": 0.58, "energy": 0.10, "overhead": 0.10}


def working_days() -> list[date]:
    days, day = [], START
    while day <= END:
        if day.weekday() != 6 and day not in HOLIDAYS:
            days.append(day)
        day += timedelta(days=1)
    return days


def production(rng: random.Random) -> list[dict]:
    rows = []
    for day in working_days():
        for m in MACHINES:
            downtime = OUTAGE.get(m.id, rng.randint(*m.downtime)) if day == OUTAGE_DAY else rng.randint(*m.downtime)
            cycle = round(rng.uniform(*m.cycle), 1)
            run = PLANNED - downtime
            total = int(run * 60 / cycle)
            good = total - round(total * rng.uniform(*m.scrap))
            rows.append(
                {"date": day, "machine": m.id, "line": m.line, "planned": PLANNED, "downtime": downtime,
                 "total": total, "good": good, "produced": good, "cycle": cycle}
            )
    return rows


def costs(rng: random.Random, prod: list[dict]) -> list[dict]:
    """Un registro por máquina y mes (fecha de cierre) con el costo real objetivo por unidad."""
    rows = []
    for month in (10, 11, 12):
        close = (date(2024, month + 1, 1) - timedelta(days=1)) if month < 12 else date(2024, 12, 31)
        for m in MACHINES:
            units = sum(r["produced"] for r in prod if r["machine"] == m.id and r["date"].month == month)
            total = m.target_cpu * units
            split = {k: round(total * v * rng.uniform(0.98, 1.02), 2) for k, v in COST_SPLIT.items()}
            rows.append({"date": close, "machine": m.id, **split})
    return rows


def expected(prod: list[dict], cost_rows: list[dict]) -> dict:
    machines = {}
    for m in MACHINES:
        rows = [r for r in prod if r["machine"] == m.id]
        planned = sum(r["planned"] for r in rows)
        downtime = sum(r["downtime"] for r in rows)
        total = sum(r["total"] for r in rows)
        good = sum(r["good"] for r in rows)
        produced = sum(r["produced"] for r in rows)
        run = planned - downtime
        ideal = m.std_cycle * total / 60
        cost = sum(c["labor"] + c["material"] + c["energy"] + c["overhead"] for c in cost_rows if c["machine"] == m.id)
        availability, performance, quality = run / planned, ideal / run, good / total
        machines[m.id] = {
            "line": m.line, "planned": planned, "downtime": downtime, "total": total, "good": good,
            "produced": produced, "availability": availability, "performance": performance,
            "quality": quality, "oee": availability * performance * quality,
            "scrapRate": (total - good) / total, "downtimeRatio": downtime / planned,
            "capacityUtilization": ideal / planned, "costPerUnit": cost / produced,
            "standardCost": m.std_cost, "downtimeCost": downtime * m.cost_per_minute,
            "scrapCost": (total - good) * m.std_cost, "speedLossCost": max(0.0, run - ideal) * m.cost_per_minute,
        }

    flagged = {
        "HIGH_DOWNTIME": [k for k, v in machines.items() if v["downtimeRatio"] > HIGH_DOWNTIME],
        "HIGH_SCRAP": [k for k, v in machines.items() if v["scrapRate"] > HIGH_SCRAP],
        "LOW_OEE": [k for k, v in machines.items() if v["oee"] < LOW_OEE],
        "COST_PER_UNIT_ABOVE_STANDARD": [
            k for k, v in machines.items() if v["costPerUnit"] > v["standardCost"] * (1 + COST_TOLERANCE)
        ],
    }
    attributed = dict.fromkeys(machines, 0.0)
    impacts = {}
    impacts["HIGH_DOWNTIME"] = sum(machines[k]["downtimeCost"] for k in flagged["HIGH_DOWNTIME"])
    for k in flagged["HIGH_DOWNTIME"]:
        attributed[k] += machines[k]["downtimeCost"]
    impacts["HIGH_SCRAP"] = sum(machines[k]["scrapCost"] for k in flagged["HIGH_SCRAP"])
    for k in flagged["HIGH_SCRAP"]:
        attributed[k] += machines[k]["scrapCost"]
    oee_loss = 0.0
    for k in flagged["LOW_OEE"]:
        v = machines[k]
        loss = v["speedLossCost"]
        loss += 0 if k in flagged["HIGH_DOWNTIME"] else v["downtimeCost"]
        loss += 0 if k in flagged["HIGH_SCRAP"] else v["scrapCost"]
        attributed[k] += loss
        oee_loss += loss
    impacts["LOW_OEE"] = oee_loss
    impacts["COST_PER_UNIT_ABOVE_STANDARD"] = sum(
        max(0.0, (machines[k]["costPerUnit"] - machines[k]["standardCost"]) * machines[k]["produced"] - attributed[k])
        for k in flagged["COST_PER_UNIT_ABOVE_STANDARD"]
    )
    return {
        "period": {"from": START.isoformat(), "to": END.isoformat(), "days": len(working_days())},
        "outageDay": OUTAGE_DAY.isoformat(),
        "machines": machines,
        "problems": {code: {"machines": flagged[code], "impact": round(impacts[code], 2)} for code in flagged},
    }


def write_xlsx(path: Path, sheet: str, headers: list[str], rows: list[list]) -> None:
    wb = Workbook()
    ws = wb.active
    ws.title = sheet
    ws.append(headers)
    for cell in ws[1]:
        cell.font = Font(bold=True)
    for row in rows:
        ws.append(row)
    for cell in ws["A"][1:]:
        if isinstance(cell.value, date):
            cell.number_format = "yyyy-mm-dd"
    wb.save(path)


def main() -> None:
    rng = random.Random(2024)
    OUT.mkdir(parents=True, exist_ok=True)
    prod = production(rng)
    cost_rows = costs(rng, prod)

    write_xlsx(
        OUT / "produccion.xlsx", "Produccion",
        ["Fecha", "Máquina", "Línea", "Minutos planificados", "Minutos de parada", "Unidades totales",
         "Unidades buenas", "Unidades producidas", "Tiempo de ciclo (s)"],
        [[r["date"], r["machine"], r["line"], r["planned"], r["downtime"], r["total"], r["good"], r["produced"], r["cycle"]]
         for r in prod],
    )
    write_xlsx(
        OUT / "costos.xlsx", "Costos",
        ["Fecha", "Máquina", "Mano de obra", "Materiales", "Energía", "Gastos generales"],
        [[c["date"], c["machine"], c["labor"], c["material"], c["energy"], c["overhead"]] for c in cost_rows],
    )
    write_xlsx(
        OUT / "estandares.xlsx", "Estandares",
        ["Máquina", "Proceso", "Ciclo estándar (s)", "Costo estándar por unidad", "Costo por minuto"],
        [[m.id, m.process, m.std_cycle, m.std_cost, m.cost_per_minute] for m in MACHINES],
    )

    # CSV con encabezados de la especificación (en inglés) y filas inválidas para probar la validación.
    with (OUT / "produccion_con_errores.csv").open("w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(["date", "machine_id", "line_id", "planned_minutes", "downtime_minutes", "total_units",
                         "good_units", "units_produced", "cycle_time"])
        writer.writerow(["2024-10-01", "MAQ-01", "LINEA-1", 960, 40, 1700, 1670, 1670, 32.4])
        writer.writerow(["02/10/2024", "MAQ-01", "LINEA-1", 960, 35, 1710, 1680, "", 32.4])  # fecha DD/MM/AAAA, sin producidas
        writer.writerow(["2024-10-03", "MAQ-01", "LINEA-1", 960, 30, 1500, 1600, 1600, 32.4])  # buenas > totales
        writer.writerow(["2024-10-04", "", "LINEA-1", 960, 30, 1500, 1450, 1450, 32.4])  # sin máquina
        writer.writerow(["no es fecha", "MAQ-01", "LINEA-1", 960, 30, 1500, 1450, 1450, 32.4])  # fecha inválida
        writer.writerow(["2024-10-05", "MAQ-01", "LINEA-1", 960, 1000, 1500, 1450, 1450, 32.4])  # parada > planificado
        writer.writerow(["2024-10-01", "MAQ-01", "LINEA-1", 960, 42, 1690, 1660, 1660, 32.4])  # duplicado

    (OUT / "expected_industrial.json").write_text(json.dumps(expected(prod, cost_rows), indent=2, ensure_ascii=False) + "\n")
    print(f"Generados {len(prod)} registros de producción y {len(cost_rows)} de costos en {OUT}")


if __name__ == "__main__":
    main()
