#!/usr/bin/env python3
"""Genera libros contables SINTÉTICOS con la estructura de los archivos de los clientes.

Uso (desde la raíz del repositorio):
    python3 backend/tests/TestData/generate_sample_workbooks.py

Requisitos: openpyxl y LibreOffice (``soffice``) para calcular las fórmulas y producir ``.xls``.

La estructura (nombres de hoja, títulos, secciones, etiquetas, columnas desplazadas, subtotales,
fórmulas, notas, nómina, ventas mensuales, balance de comprobación y hojas fiscales) sigue
``docs/especificaciones/FORMATO_EXCEL_ENTRADA.md``. **Todos los nombres y valores son
inventados**: empresa ficticia, empleados "EMPLEADO 001" y cédulas "000-0000000-0".

Salida en ``backend/sample-data/``: los libros, ``expected_values.json`` (valores esperados que
leen las pruebas .NET) y ``README.md`` (descripción de cada archivo).
"""

from __future__ import annotations

import copy
import json
import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass, field
from pathlib import Path

from openpyxl import Workbook
from openpyxl.styles import Alignment, Font
from openpyxl.utils import get_column_letter

ROOT = Path(__file__).resolve().parents[3]
OUT_DIR = ROOT / "backend" / "sample-data"

COMPANY = "INDUSTRIAS FICTICIAS DEL CARIBE, S.R.L."
COMPANY_VARIANT = "INDUSTRIAS FICTICIAS DEL CARIBE, SRL"
MONTHS = [
    "ENERO",
    "FEBRERO",
    "MARZO",
    "ABRIL",
    "MAYO",
    "JUNIO",
    "JULIO",
    "AGOSTO",
    "SEPTIEMBRE",
    "OCTUBRE",
    "NOVIEMBRE",
    "DICIEMBRE",
]
ISR_RATE = 0.27


def r2(value: float) -> float:
    return round(value + 0.0, 2)


# ---------------------------------------------------------------------------------------------
# Datos inventados por año (entradas). Todo lo demás se deriva y queda consistente.
# ---------------------------------------------------------------------------------------------
INPUTS: dict[int, dict[str, float]] = {
    2022: {
        "gross_sales": 158_900_000,
        "discounts": 2_542_400,
        "inv_ini_mp": 6_900_000,
        "mp_import": 49_800_000,
        "mp_local": 26_400_000,
        "inv_fin_mp": 7_400_000,
        "lab_sueldos": 12_100_000,
        "lab_regalia": 1_008_333.33,
        "lab_tss": 1_790_800,
        "lab_infotep": 121_000,
        "fab_energia": 7_320_000,
        "fab_mant": 2_540_000,
        "fab_comb": 1_720_000,
        "fab_dep": 4_200_000,
        "inv_ini_pt": 5_600_000,
        "inv_fin_pt": 5_900_000,
        "c_sueldos": 8_300_000,
        "c_regalia": 691_666.67,
        "c_bonif": 900_000,
        "c_tss": 1_228_400,
        "c_infotep": 83_000,
        "c_preaviso": 120_000,
        "g_honorarios": 1_100_000,
        "g_energia": 1_090_000,
        "g_comb": 600_000,
        "g_rep": 350_000,
        "g_mant": 430_000,
        "g_rep_veh": 300_000,
        "g_seguros": 680_000,
        "g_tel": 280_000,
        "g_alquiler": 2_160_000,
        "g_otros": 390_000,
        "dep_amort": 2_050_000,
        "fin_exp": 3_760_000,
        "caja_chica": 100_000,
        "banco1": 4_900_000,
        "clientes": 14_600_000,
        "cxc_empleados": 250_000,
        "cxc_otras": 580_000,
        "provision": 730_000,
        "prepagados": 500_000,
        "terreno": 12_000_000,
        "edificaciones": 28_500_000,
        "maquinarias": 36_900_000,
        "vehiculos": 5_100_000,
        "dep_acum": 17_850_000,
        "depositos": 450_000,
        "inversiones": 1_000_000,
        "prest_banco": 7_500_000,
        "linea_credito": 900_000,
        "prov_locales": 8_100_000,
        "prov_exterior": 5_300_000,
        "itbis_pagar": 980_000,
        "ret_isr": 270_000,
        "tss_pagar": 390_000,
        "prest_lp": 26_000_000,
        "capital_social": 20_000_000,
        "reserva_legal": 1_000_000,
        "dividends": 1_500_000,
    },
    2023: {
        "gross_sales": 172_300_000,
        "discounts": 3_101_400,
        "inv_ini_mp": 7_400_000,
        "mp_import": 56_950_000,
        "mp_local": 30_120_000,
        "inv_fin_mp": 8_250_000,
        "lab_sueldos": 13_200_000,
        "lab_regalia": 1_100_000,
        "lab_tss": 1_953_600,
        "lab_infotep": 132_000,
        "fab_energia": 8_640_000,
        "fab_mant": 2_980_000,
        "fab_comb": 1_950_000,
        "fab_dep": 4_500_000,
        "inv_ini_pt": 5_900_000,
        "inv_fin_pt": 6_400_000,
        "c_sueldos": 8_900_000,
        "c_regalia": 741_666.67,
        "c_bonif": 980_000,
        "c_tss": 1_317_200,
        "c_infotep": 89_000,
        "c_preaviso": 150_000,
        "g_honorarios": 1_180_000,
        "g_energia": 1_210_000,
        "g_comb": 640_000,
        "g_rep": 385_000,
        "g_mant": 470_000,
        "g_rep_veh": 330_000,
        "g_seguros": 720_000,
        "g_tel": 295_000,
        "g_alquiler": 2_280_000,
        "g_otros": 410_000,
        "dep_amort": 2_200_000,
        "fin_exp": 3_480_000,
        "caja_chica": 120_000,
        "banco1": 5_430_000,
        "clientes": 16_250_000,
        "cxc_empleados": 280_000,
        "cxc_otras": 610_000,
        "provision": 812_500,
        "prepagados": 540_000,
        "terreno": 12_000_000,
        "edificaciones": 28_500_000,
        "maquinarias": 41_200_000,
        "vehiculos": 5_700_000,
        "dep_acum": 24_300_000,
        "depositos": 450_000,
        "inversiones": 1_500_000,
        "prest_banco": 6_500_000,
        "linea_credito": 1_200_000,
        "prov_locales": 8_940_000,
        "prov_exterior": 5_880_000,
        "itbis_pagar": 1_105_000,
        "ret_isr": 290_000,
        "tss_pagar": 420_000,
        "prest_lp": 22_000_000,
        "capital_social": 20_000_000,
        "reserva_legal": 1_500_000,
        "dividends": 2_000_000,
    },
    2024: {
        "gross_sales": 185_450_000,
        "discounts": 3_709_000,
        "inv_ini_mp": 8_250_000,
        "mp_import": 61_300_000,
        "mp_local": 32_780_000,
        "inv_fin_mp": 9_130_000,
        "lab_sueldos": 14_400_000,
        "lab_regalia": 1_200_000,
        "lab_tss": 2_131_200,
        "lab_infotep": 144_000,
        "fab_energia": 9_870_000,
        "fab_mant": 3_450_000,
        "fab_comb": 2_210_000,
        "fab_dep": 4_800_000,
        "inv_ini_pt": 6_400_000,
        "inv_fin_pt": 7_050_000,
        "c_sueldos": 9_600_000,
        "c_regalia": 800_000,
        "c_bonif": 1_150_000,
        "c_tss": 1_420_800,
        "c_infotep": 96_000,
        "c_preaviso": 380_000,
        "g_honorarios": 1_250_000,
        "g_energia": 1_340_000,
        "g_comb": 690_000,
        "g_rep": 420_000,
        "g_mant": 510_000,
        "g_rep_veh": 365_000,
        "g_seguros": 780_000,
        "g_tel": 310_000,
        "g_alquiler": 2_400_000,
        "g_otros": 455_000,
        "dep_amort": 2_350_000,
        "fin_exp": 3_120_000,
        "caja_chica": 150_000,
        "banco1": 6_850_000,
        "clientes": 18_400_000,
        "cxc_empleados": 320_000,
        "cxc_otras": 540_000,
        "provision": 920_000,
        "prepagados": 610_000,
        "terreno": 12_000_000,
        "edificaciones": 28_500_000,
        "maquinarias": 46_800_000,
        "vehiculos": 6_300_000,
        "dep_acum": 31_450_000,
        "depositos": 450_000,
        "inversiones": 2_000_000,
        "prest_banco": 5_000_000,
        "linea_credito": 1_800_000,
        "prov_locales": 9_850_000,
        "prov_exterior": 6_420_000,
        "itbis_pagar": 1_240_000,
        "ret_isr": 310_000,
        "tss_pagar": 455_000,
        "prest_lp": 18_000_000,
        "capital_social": 20_000_000,
        "reserva_legal": 2_000_000,
        "dividends": 3_000_000,
    },
}

# El 3.er año (2022) existe solo para la variante de 3 años. La variante base usa 2023 y 2024.
RESERVE_TRANSFER = 500_000
OPENING_RETAINED = 24_000_000  # beneficios acumulados al inicio de 2022 (primer año de la serie)


@dataclass
class YearData:
    year: int
    v: dict[str, float] = field(default_factory=dict)


def derive(years: list[int]) -> dict[int, dict[str, float]]:
    """Calcula los totales de cada año. ``years`` en orden ascendente."""
    result: dict[int, dict[str, float]] = {}
    retained_start = OPENING_RETAINED
    for year in years:
        v = dict(INPUTS[year])
        v["net_sales"] = r2(v["gross_sales"] - v["discounts"])
        v["mp_used"] = r2(v["inv_ini_mp"] + v["mp_import"] + v["mp_local"] - v["inv_fin_mp"])
        v["labor_total"] = r2(v["lab_sueldos"] + v["lab_regalia"] + v["lab_tss"] + v["lab_infotep"])
        v["fab_total"] = r2(v["fab_energia"] + v["fab_mant"] + v["fab_comb"] + v["fab_dep"])
        v["prod_cost"] = r2(v["mp_used"] + v["labor_total"] + v["fab_total"])
        v["cogs"] = r2(v["prod_cost"] + v["inv_ini_pt"] - v["inv_fin_pt"])
        v["gross_profit"] = r2(v["net_sales"] - v["cogs"])
        v["comp_total"] = r2(
            sum(v[k] for k in ("c_sueldos", "c_regalia", "c_bonif", "c_tss", "c_infotep", "c_preaviso"))
        )
        v["admin_total"] = r2(
            sum(
                v[k]
                for k in (
                    "g_honorarios",
                    "g_energia",
                    "g_comb",
                    "g_rep",
                    "g_mant",
                    "g_rep_veh",
                    "g_seguros",
                    "g_tel",
                    "g_alquiler",
                    "g_otros",
                )
            )
        )
        v["opex_total"] = r2(v["comp_total"] + v["admin_total"] + v["dep_amort"])
        v["operating_profit"] = r2(v["gross_profit"] - v["opex_total"])
        v["ebt"] = r2(v["operating_profit"] - v["fin_exp"])
        v["isr"] = r2(v["ebt"] * ISR_RATE)
        v["net_income"] = r2(v["ebt"] - v["isr"])
        v["ebitda"] = r2(v["ebt"] + v["fin_exp"] + v["dep_amort"] + v["fab_dep"])

        # Balance
        v["ppe_cost"] = r2(v["terreno"] + v["edificaciones"] + v["maquinarias"] + v["vehiculos"])
        v["ppe_net"] = r2(v["ppe_cost"] - v["dep_acum"])
        v["cxc_total"] = r2(v["clientes"] + v["cxc_empleados"] + v["cxc_otras"] - v["provision"])
        v["otros_activos"] = r2(v["depositos"] + v["inversiones"])
        v["prestamos_total"] = r2(v["prest_banco"] + v["linea_credito"])
        v["cxp_total"] = r2(v["prov_locales"] + v["prov_exterior"])
        v["acum_total"] = r2(v["itbis_pagar"] + v["ret_isr"] + v["tss_pagar"])
        v["pasivo_corriente"] = r2(v["prestamos_total"] + v["cxp_total"] + v["acum_total"])
        v["total_liabilities"] = r2(v["pasivo_corriente"] + v["prest_lp"])

        # Patrimonio: beneficios acumulados = inicio − reserva − dividendos (el del período va aparte).
        v["retained_start"] = r2(retained_start)
        v["retained"] = r2(retained_start - RESERVE_TRANSFER - v["dividends"])
        v["equity"] = r2(v["capital_social"] + v["reserva_legal"] + v["retained"] + v["net_income"])
        v["total_liab_equity"] = r2(v["total_liabilities"] + v["equity"])

        # El banco 2 cuadra el balance (efectivo = pasivos + capital − resto de activos).
        others = (
            v["caja_chica"]
            + v["banco1"]
            + v["cxc_total"]
            + v["inv_fin_pt"]
            + v["inv_fin_mp"]
            + v["prepagados"]
            + v["ppe_net"]
            + v["otros_activos"]
        )
        v["banco2"] = r2(v["total_liab_equity"] - others)
        if v["banco2"] <= 0:
            raise ValueError(f"Datos inventados inconsistentes en {year}: banco2={v['banco2']}")
        v["cash"] = r2(v["caja_chica"] + v["banco1"] + v["banco2"])
        v["current_assets"] = r2(v["cash"] + v["cxc_total"] + v["inv_fin_pt"] + v["inv_fin_mp"] + v["prepagados"])
        v["total_assets"] = r2(v["current_assets"] + v["ppe_net"] + v["otros_activos"])
        v["inventory"] = r2(v["inv_fin_pt"] + v["inv_fin_mp"])
        v["financial_debt"] = r2(v["prestamos_total"] + v["prest_lp"])
        if abs(v["total_assets"] - v["total_liab_equity"]) > 0.005:
            raise ValueError("El balance inventado no cuadra")

        retained_start = v["retained"] + v["net_income"]
        result[year] = v
    return result


# ---------------------------------------------------------------------------------------------
# Escritura de hojas
# ---------------------------------------------------------------------------------------------
@dataclass
class Variant:
    name: str
    years: list[int]
    description: str
    ext: tuple[str, ...] = ("xls",)
    company_name_mismatch: bool = False
    balance_mismatch: float = 0.0
    note_mismatch: bool = False
    equity_mismatch: bool = False
    trial_balance_broken: bool = False
    subtotal_mismatch: bool = False
    income_single_year: bool = False
    invalid_years: bool = False
    expected_warnings: list[str] = field(default_factory=list)


class SheetWriter:
    """Escribe una hoja con títulos, fila de años y líneas; los totales son fórmulas."""

    def __init__(self, ws, years: list[int], value_cols: list[int], company: str):
        self.ws = ws
        self.years = years  # en el orden de las columnas (año actual primero)
        self.value_cols = value_cols
        self.row = 1
        self.lines: list[dict] = []
        ws.cell(1, 1, company).font = Font(bold=True)

    def title(self, *texts: str) -> None:
        for text in texts:
            self.row += 1
            cell = self.ws.cell(self.row, 1, text)
            cell.font = Font(bold=True)
            cell.alignment = Alignment(horizontal="center")
            last = max(self.value_cols)
            self.ws.merge_cells(start_row=self.row, start_column=1, end_row=self.row, end_column=last)

    def year_row(self, invalid: bool = False) -> None:
        self.row += 2
        for col, year in zip(self.value_cols, self.years, strict=True):
            self.ws.cell(self.row, col, year - 174 if invalid else year).font = Font(bold=True)
        self.row += 1

    def section(self, text: str, col: int = 1) -> None:
        self.row += 1
        self.ws.cell(self.row, col, text).font = Font(bold=True)

    def blank(self) -> None:
        self.row += 1

    def line(
        self,
        col: int,
        label: str | None,
        values: dict[int, float] | None = None,
        formula: str | None = None,
        refs: list[tuple[int, int]] | None = None,
        override: dict[int, float] | None = None,
        bold: bool = False,
    ) -> int:
        """Agrega una línea. ``formula``: 'sum' o 'expr' con ``refs`` [(fila, signo)]."""
        self.row += 1
        if label is not None:
            cell = self.ws.cell(self.row, col, label)
            cell.font = Font(bold=bold)
        for vcol, year in zip(self.value_cols, self.years, strict=True):
            letter = get_column_letter(vcol)
            if override and year in override:
                value: object = override[year]
            elif formula and refs:
                parts = [f"{'+' if sign > 0 else '-'}{letter}{ref}" for ref, sign in refs]
                value = "=" + "".join(parts).lstrip("+")
            elif values is not None:
                value = values.get(year)
            else:
                value = None
            if value is not None:
                c = self.ws.cell(self.row, vcol, value)
                c.number_format = "#,##0.00;(#,##0.00)"
        self.lines.append({"row": self.row, "label": label})
        return self.row


def vals(data: dict[int, dict[str, float]], key: str, years: list[int]) -> dict[int, float]:
    return {y: data[y][key] for y in years}


def write_balance(wb, data, years, variant: Variant, expected: dict) -> None:
    ws = wb.create_sheet("BALANCE")
    w = SheetWriter(ws, years, [5, 7] if len(years) == 2 else [5, 7, 9], COMPANY)
    w.title("BALANCES GENERALES", "AL 31 DE DICIEMBRE DE " + " Y ".join(str(y) for y in years), "(Valores en RD$)")
    w.year_row(variant.invalid_years)
    V = lambda key: vals(data, key, years)  # noqa: E731
    rows: dict[str, int] = {}
    w.section("ACTIVOS CORRIENTES")
    rows["cash"] = w.line(3, "Efectivo en Cajas & Bancos (nota 3)", V("cash"))
    rows["cxc"] = w.line(3, "Cuentas por Cobrar (nota 4)", V("cxc_total"))
    rows["inv_pt"] = w.line(3, "Inventario Productos Terminados", V("inv_fin_pt"))
    rows["inv_mp"] = w.line(3, "Inventario Materia Prima", V("inv_fin_mp"))
    rows["prep"] = w.line(3, "Gastos pagados por anticipado", V("prepagados"))
    rows["ca"] = w.line(
        3,
        "Total corriente",
        formula="sum",
        refs=[(rows[k], 1) for k in ("cash", "cxc", "inv_pt", "inv_mp", "prep")],
        bold=True,
    )
    w.blank()
    w.section("PROPIEDAD PLANTA & EQUIPOS")
    rows["ppe"] = w.line(3, "Propiedad, planta y equipo, neto (nota 5)", V("ppe_net"))
    w.blank()
    w.section("OTROS ACTIVOS")
    rows["oa"] = w.line(3, "Otros activos (nota 6)", V("otros_activos"))
    w.blank()
    rows["ta"] = w.line(
        3, "Total Activos", formula="sum", refs=[(rows["ca"], 1), (rows["ppe"], 1), (rows["oa"], 1)], bold=True
    )
    w.blank()
    w.section("PASIVO CORRIENTE")
    rows["prest"] = w.line(3, "Préstamos por Pagar (nota 7)", V("prestamos_total"))
    rows["cxp"] = w.line(3, "Cuentas por Pagar (nota 8)", V("cxp_total"))
    rows["acum"] = w.line(3, "Acumulaciones y retenciones por pagar (nota 9)", V("acum_total"))
    rows["pc"] = w.line(
        3, "Total corriente", formula="sum", refs=[(rows[k], 1) for k in ("prest", "cxp", "acum")], bold=True
    )
    w.blank()
    w.section("PASIVO A LARGO PLAZO")
    rows["lp"] = w.line(3, "Préstamos a largo plazo (nota 10)", V("prest_lp"))
    w.blank()
    w.section("CAPITAL")
    rows["cs"] = w.line(3, "Capital social", V("capital_social"))
    rows["rl"] = w.line(3, "Reserva legal", V("reserva_legal"))
    rows["ba"] = w.line(3, "Beneficios acumulados", V("retained"))
    rows["bp"] = w.line(3, "Beneficio del período", V("net_income"))
    rows["tc"] = w.line(
        3, "Total capital", formula="sum", refs=[(rows[k], 1) for k in ("cs", "rl", "ba", "bp")], bold=True
    )
    w.blank()
    override = None
    if variant.balance_mismatch:
        override = {years[0]: r2(data[years[0]]["total_liab_equity"] + variant.balance_mismatch)}
    rows["tpc"] = w.line(
        3,
        "Total Pasivos & Capital",
        formula="sum",
        refs=[(rows["pc"], 1), (rows["lp"], 1), (rows["tc"], 1)],
        override=override,
        bold=True,
    )
    expected["rows"]["BALANCE"] = rows


def write_income(wb, data, years, variant: Variant, expected: dict) -> None:
    ws = wb.create_sheet("g&P")
    ys = years[:1] if variant.income_single_year else years
    cols = [6, 8, 10][: len(ys)]
    w = SheetWriter(ws, ys, cols, COMPANY)
    w.title("ESTADOS DE GANANCIAS Y PERDIDAS", "POR LOS AÑOS TERMINADOS AL 31 DE DICIEMBRE", "(Valores en RD$)")
    w.year_row(variant.invalid_years)
    V = lambda key: vals(data, key, ys)  # noqa: E731
    rows: dict[str, int] = {}
    w.section("Ingresos:", col=3)
    rows["gs"] = w.line(4, "Ventas netas", V("gross_sales"))
    rows["disc"] = w.line(4, "Menos: Descuentos sobre ventas", V("discounts"))
    rows["ns"] = w.line(4, None, formula="expr", refs=[(rows["gs"], 1), (rows["disc"], -1)])
    rows["cogs"] = w.line(4, "Costos de Ventas (nota 12)", V("cogs"))
    rows["gp"] = w.line(3, "Ganancia bruta", formula="expr", refs=[(rows["ns"], 1), (rows["cogs"], -1)], bold=True)
    w.blank()
    w.section("Gastos de Operaciones:", col=3)
    rows["comp"] = w.line(4, "Compensaciones al personal (nota 11)", V("comp_total"))
    rows["adm"] = w.line(4, "Gastos Administrativos (nota 13)", V("admin_total"))
    rows["da"] = w.line(4, "Depreciación & Amortización", V("dep_amort"))
    override = None
    if variant.subtotal_mismatch:
        override = {ys[0]: r2(data[ys[0]]["opex_total"] + 250_000)}
    rows["opex"] = w.line(
        3,
        "Total gastos de operaciones",
        formula="sum",
        refs=[(rows[k], 1) for k in ("comp", "adm", "da")],
        override=override,
        bold=True,
    )
    # En la variante de subtotal erróneo, la ganancia se calcula con las líneas (el error no se propaga).
    opex_refs = [(rows[k], -1) for k in ("comp", "adm", "da")] if variant.subtotal_mismatch else [(rows["opex"], -1)]
    rows["op"] = w.line(3, "Ganancia en operaciones", formula="expr", refs=[(rows["gp"], 1), *opex_refs])
    rows["fin"] = w.line(4, "Gastos Financieros", V("fin_exp"))
    rows["ebt"] = w.line(
        3, "Ingresos netos antes impuesto s/renta", formula="expr", refs=[(rows["op"], 1), (rows["fin"], -1)]
    )
    rows["isr"] = w.line(4, "Impuesto sobre la renta (nota 15)", V("isr"))
    rows["ni"] = w.line(3, "Ingresos Netos", formula="expr", refs=[(rows["ebt"], 1), (rows["isr"], -1)], bold=True)
    expected["rows"]["g&P"] = rows


def write_equity(wb, data, years, variant: Variant) -> None:
    ws = wb.create_sheet("EPC")
    ws.cell(1, 1, COMPANY).font = Font(bold=True)
    ws.cell(2, 1, "ESTADOS DE CAMBIOS EN EL PATRIMONIO DE LOS SOCIOS").font = Font(bold=True)
    ws.cell(3, 1, "(Valores en RD$)")
    headers = ["", "", "Capital social", "Reserva legal", "Beneficios acumulados", "Total"]
    for i, h in enumerate(headers, start=1):
        if h:
            ws.cell(5, i, h).font = Font(bold=True)
    asc = sorted(years)
    first = data[asc[0]]
    row = 6
    cs, rl = first["capital_social"], first["reserva_legal"] - RESERVE_TRANSFER
    ba = first["retained_start"]

    def put(label: str, a: float | None, b: float | None, c: float | None) -> None:
        nonlocal row
        ws.cell(row, 1, label)
        for col, value in ((3, a), (4, b), (5, c)):
            if value is not None:
                ws.cell(row, col, value).number_format = "#,##0.00;(#,##0.00)"
        ws.cell(row, 6, f"=SUM(C{row}:E{row})").number_format = "#,##0.00;(#,##0.00)"
        row += 1

    put(f"Saldo al 31 de diciembre de {asc[0] - 1}", cs, rl, ba)
    for year in asc:
        d = data[year]
        ni = d["net_income"]
        if variant.equity_mismatch and year == asc[-1]:
            ni = r2(ni - 1_000_000)
        put(f"Beneficio neto del año {year}", None, None, ni)
        put("Transferencia a reserva legal", None, RESERVE_TRANSFER, -RESERVE_TRANSFER)
        put("Dividendos pagados", None, None, -d["dividends"])
        ba = r2(ba + ni - RESERVE_TRANSFER - d["dividends"])
        rl = r2(rl + RESERVE_TRANSFER)
        put(f"Saldo al 31 de diciembre de {year}", cs, rl, ba)


def note_writer(wb, name: str, years: list[int], cols: list[int], company: str, invalid: bool) -> SheetWriter:
    ws = wb.create_sheet(name)
    w = SheetWriter(ws, years, cols, company)
    w.title("NOTAS A LOS ESTADOS FINANCIEROS", "AL 31 DE DICIEMBRE DE " + " Y ".join(str(y) for y in years))
    w.year_row(invalid)
    return w


def note_block(
    w: SheetWriter, title: str, label_col: int, items: list[tuple[str, dict[int, float], int]], total_label: str | None
) -> int:
    """Escribe una nota: título en col. A, detalles y un total (fórmula). Devuelve la fila del total."""
    w.blank()
    w.section(title)
    refs = []
    for label, values, sign in items:
        refs.append((w.line(label_col, label, values), sign))
    return w.line(label_col, total_label, formula="expr", refs=refs, bold=True)


def write_notes(wb, data, years, variant: Variant, expected: dict) -> None:
    invalid = variant.invalid_years
    V = lambda key: vals(data, key, years)  # noqa: E731
    n = len(years)

    # nota1: efectivo, cuentas por cobrar, PPE y otros activos (años en G/I)
    w = note_writer(wb, "nota1", years, [7, 9, 11][:n], COMPANY, invalid)
    note_block(
        w,
        "3. EFECTIVO EN CAJAS & BANCOS",
        3,
        [
            ("Caja chica", V("caja_chica"), 1),
            ("Banco Ficticio Comercial, Cta. 000-000001-1", V("banco1"), 1),
            ("Banco Imaginario de Reservas, Cta. 000-000002-2", V("banco2"), 1),
        ],
        None,
    )
    clientes = V("clientes")
    if variant.note_mismatch:
        clientes = {**clientes, years[0]: r2(clientes[years[0]] + 75_000)}
    expected["rows"]["nota1_cxc_total"] = note_block(
        w,
        "4. CUENTAS POR COBRAR",
        3,
        [
            ("Clientes", clientes, 1),
            ("Empleados", V("cxc_empleados"), 1),
            ("Otras cuentas por cobrar", V("cxc_otras"), 1),
            ("Menos: Provisión para cuentas incobrables", V("provision"), -1),
        ],
        "Total cuentas por cobrar",
    )
    w.blank()
    w.section("5. PROPIEDAD, PLANTA Y EQUIPOS")
    r1 = w.line(3, "Terreno", V("terreno"))
    r2_ = w.line(3, "Edificaciones", V("edificaciones"))
    r3 = w.line(3, "Maquinarias & equipos", V("maquinarias"))
    r4 = w.line(3, "Vehículos", V("vehiculos"))
    tc = w.line(3, "Total costo", formula="sum", refs=[(r1, 1), (r2_, 1), (r3, 1), (r4, 1)], bold=True)
    da = w.line(3, "Menos: Depreciación acumulada", V("dep_acum"))
    w.line(3, None, formula="expr", refs=[(tc, 1), (da, -1)], bold=True)
    note_block(
        w,
        "6. OTROS ACTIVOS",
        3,
        [
            ("Depósitos en garantía", V("depositos"), 1),
            ("Inversiones en certificados financieros", V("inversiones"), 1),
        ],
        "Total otros activos",
    )

    # nota2: préstamos, CxP, acumulaciones, deuda a largo plazo y cálculo del ISR (años en G/I)
    w = note_writer(wb, "nota2", years, [7, 9, 11][:n], COMPANY, invalid)
    note_block(
        w,
        "7. PRESTAMOS POR PAGAR",
        3,
        [
            ("Banco Ficticio Comercial, préstamo comercial", V("prest_banco"), 1),
            ("Línea de crédito", V("linea_credito"), 1),
        ],
        "Total",
    )
    note_block(
        w,
        "8. CUENTAS POR PAGAR",
        3,
        [
            ("Proveedores locales", V("prov_locales"), 1),
            ("Proveedores del exterior", V("prov_exterior"), 1),
        ],
        "Total cuentas por pagar",
    )
    note_block(
        w,
        "9. ACUMULACIONES Y RETENCIONES POR PAGAR",
        3,
        [
            ("ITBIS por pagar", V("itbis_pagar"), 1),
            ("Retenciones ISR asalariados (IR-3)", V("ret_isr"), 1),
            ("TSS por pagar", V("tss_pagar"), 1),
        ],
        "Total",
    )
    note_block(
        w,
        "10. PRESTAMOS A LARGO PLAZO",
        3,
        [
            ("Banco Imaginario de Reservas, préstamo hipotecario", V("prest_lp"), 1),
        ],
        "Total",
    )
    w.blank()
    w.section("15. IMPUESTO SOBRE LA RENTA")
    w.line(3, "Ganancia antes del impuesto sobre la renta", V("ebt"))
    w.line(3, "Tasa del impuesto (27%)", None)
    w.line(3, "Impuesto sobre la renta", V("isr"))

    # nota3: compensaciones y gastos generales (etiquetas en col. D, años en L/N)
    company3 = COMPANY_VARIANT if variant.company_name_mismatch else COMPANY
    w = note_writer(wb, "nota3", years, [12, 14, 16][:n], company3, invalid)
    note_block(
        w,
        "11. COMPENSACIONES AL PERSONAL",
        4,
        [
            ("Sueldos", V("c_sueldos"), 1),
            ("Regalía Pascual", V("c_regalia"), 1),
            ("Bonificaciones", V("c_bonif"), 1),
            ("Seguridad social (TSS)", V("c_tss"), 1),
            ("Infotep", V("c_infotep"), 1),
            ("Preaviso & Cesantía", V("c_preaviso"), 1),
        ],
        "Total compensaciones al personal",
    )
    note_block(
        w,
        "13. GASTOS GENERALES Y ADMINISTRATIVOS",
        4,
        [
            ("Honorarios profesionales", V("g_honorarios"), 1),
            ("Energía Eléctrica", V("g_energia"), 1),
            ("Combustibles", V("g_comb"), 1),
            ("Reparaciones", V("g_rep"), 1),
            ("Mantenimientos", V("g_mant"), 1),
            ("Reparaciones & Mantenimiento Vehículos", V("g_rep_veh"), 1),
            ("Seguros", V("g_seguros"), 1),
            ("Teléfonos e internet", V("g_tel"), 1),
            ("Alquileres", V("g_alquiler"), 1),
            ("Otros gastos", V("g_otros"), 1),
        ],
        "Total gastos generales y administrativos",
    )

    # nota4: costo de producción (años en G/H)
    w = note_writer(wb, "nota4", years, [7, 8, 9][:n], COMPANY, invalid)
    w.blank()
    w.section("12. COSTO DE PRODUCCION Y VENTAS")
    w.line(3, "Materia prima:", None)
    a = w.line(3, "Inventario inicial materia prima", V("inv_ini_mp"))
    b = w.line(3, "Más: Materias primas importadas", V("mp_import"))
    c = w.line(3, "Más: Compras de materias primas locales", V("mp_local"))
    d = w.line(3, "Menos: Inventario final materia prima", V("inv_fin_mp"))
    mp = w.line(3, "Total materia prima utilizada", formula="expr", refs=[(a, 1), (b, 1), (c, 1), (d, -1)], bold=True)
    w.line(3, "Labor directa:", None)
    l1 = w.line(3, "Sueldos producción", V("lab_sueldos"))
    l2 = w.line(3, "Regalía Pascual producción", V("lab_regalia"))
    l3 = w.line(3, "Seguridad social (TSS) producción", V("lab_tss"))
    l4 = w.line(3, "Infotep producción", V("lab_infotep"))
    lt = w.line(3, "Total labor directa", formula="sum", refs=[(l1, 1), (l2, 1), (l3, 1), (l4, 1)], bold=True)
    w.line(3, "Gastos de Fabricación:", None)
    f1 = w.line(3, "Energía Eléctrica", V("fab_energia"))
    f2 = w.line(3, "Reparaciones & mantenimientos", V("fab_mant"))
    f3 = w.line(3, "Combustibles y lubricantes", V("fab_comb"))
    f4 = w.line(3, "Depreciación Maquinarias & Equipos", V("fab_dep"))
    ft = w.line(3, "Total gastos de fabricación", formula="sum", refs=[(f1, 1), (f2, 1), (f3, 1), (f4, 1)], bold=True)
    cp = w.line(3, "Total costo de producción", formula="sum", refs=[(mp, 1), (lt, 1), (ft, 1)], bold=True)
    i1 = w.line(3, "Más: Inventario inicial productos terminados", V("inv_ini_pt"))
    i2 = w.line(3, "Menos: Inventario final productos terminados", V("inv_fin_pt"))
    w.line(3, "Costo de ventas", formula="expr", refs=[(cp, 1), (i1, 1), (i2, -1)], bold=True)


EMPLOYEES = [
    # (departamento, sueldo mensual, meses trabajados)
    ("PRODUCCION", 42_000, 12),
    ("PRODUCCION", 38_500, 12),
    ("PRODUCCION", 36_000, 12),
    ("PRODUCCION", 36_000, 12),
    ("PRODUCCION", 33_000, 12),
    ("PRODUCCION", 33_000, 8),
    ("PRODUCCION", 31_500, 12),
    ("PRODUCCION", 30_000, 6),
    ("ADMINISTRACION", 95_000, 12),
    ("ADMINISTRACION", 62_000, 12),
    ("ADMINISTRACION", 48_000, 12),
    ("ADMINISTRACION", 40_000, 12),
    ("VENTAS", 55_000, 12),
    ("VENTAS", 45_000, 12),
    ("VENTAS", 41_000, 10),
]


def write_roster(wb, current_year: int, expected: dict) -> None:
    ws = wb.create_sheet("REGPASCUAL")
    ws.cell(1, 1, COMPANY).font = Font(bold=True)
    ws.cell(2, 1, "REGISTRO DE REGALIA PASCUAL").font = Font(bold=True)
    ws.cell(3, 1, f"AÑO {current_year}")
    headers = ["CODIGO", "NOMBRE", "CEDULA", "CARGO", *MONTHS, "TOTAL", "REGALIA PASCUAL"]
    for i, h in enumerate(headers, start=1):
        ws.cell(5, i, h).font = Font(bold=True)
    row = 6
    number = 0
    by_dept: dict[str, dict[str, float]] = {}
    current = None
    for dept, salary, months in EMPLOYEES:
        if dept != current:
            current = dept
            ws.cell(row, 1, f"DEPARTAMENTO: {dept}").font = Font(bold=True)
            row += 1
        number += 1
        ws.cell(row, 1, f"{number:03d}")
        ws.cell(row, 2, f"EMPLEADO {number:03d}")
        ws.cell(row, 3, "000-0000000-0")
        ws.cell(row, 4, "OPERARIO" if dept == "PRODUCCION" else "AUXILIAR")
        for m in range(12):
            if m < months:
                ws.cell(row, 5 + m, salary)
        ws.cell(row, 17, f"=SUM(E{row}:P{row})")
        ws.cell(row, 18, f"=ROUND(Q{row}/12,2)")
        info = by_dept.setdefault(dept, {"count": 0, "payroll": 0.0})
        info["count"] += 1
        info["payroll"] = r2(info["payroll"] + salary * months)
        row += 1
    ws.cell(row, 2, "TOTAL GENERAL").font = Font(bold=True)
    ws.cell(row, 17, f"=SUM(Q6:Q{row - 1})")
    expected["employees"] = {
        "count": len(EMPLOYEES),
        "byDepartment": by_dept,
        "payroll": r2(sum(s * m for _, s, m in EMPLOYEES)),
    }


SEASONALITY = [0.072, 0.070, 0.081, 0.078, 0.083, 0.085, 0.084, 0.086, 0.082, 0.087, 0.090, 0.102]


def write_monthly(wb, data, year: int, expected: dict) -> None:
    ws = wb.create_sheet("CUADRO")
    ws.cell(1, 1, COMPANY).font = Font(bold=True)
    ws.cell(2, 1, "CUADRO DE VENTAS, ITBIS Y COMPRAS").font = Font(bold=True)
    ws.cell(3, 1, f"AÑO {year}")
    for i, h in enumerate(["MES", "VENTAS", "ITBIS VENTAS", "COMPRAS", "ITBIS COMPRAS"], start=1):
        ws.cell(5, i, h).font = Font(bold=True)
    sales_total = data[year]["gross_sales"]
    purchases_total = r2(data[year]["mp_import"] + data[year]["mp_local"])
    sales, purchases = [], []
    for m, share in enumerate(SEASONALITY):
        sales.append(r2(sales_total * share))
        purchases.append(r2(purchases_total * share))
    sales[-1] = r2(sales_total - sum(sales[:-1]))
    purchases[-1] = r2(purchases_total - sum(purchases[:-1]))
    for m in range(12):
        row = 6 + m
        ws.cell(row, 1, MONTHS[m].capitalize())
        ws.cell(row, 2, sales[m])
        ws.cell(row, 3, f"=ROUND(B{row}*0.18,2)")
        ws.cell(row, 4, purchases[m])
        ws.cell(row, 5, f"=ROUND(D{row}*0.18,2)")
    ws.cell(18, 1, "TOTAL").font = Font(bold=True)
    for col in "BCDE":
        ws[f"{col}18"] = f"=SUM({col}6:{col}17)"
    expected["monthly"] = {"year": year, "revenue": sales, "purchases": purchases}


def write_trial_balance(wb, data, year: int, variant: Variant, expected: dict) -> None:
    ws = wb.create_sheet("BCE COMPROBA")
    ws.cell(1, 1, COMPANY).font = Font(bold=True)
    ws.cell(2, 1, "BALANCE DE COMPROBACION").font = Font(bold=True)
    ws.cell(3, 1, f"AL 31 DE DICIEMBRE DE {year}")
    for i, h in enumerate(
        ["CUENTA", "DESCRIPCION", "ACTIVO", "PASIVO Y CAPITAL", "INGRESOS", "COSTOS Y GASTOS"], start=1
    ):
        ws.cell(5, i, h).font = Font(bold=True)
    d = data[year]
    bank = d["banco1"] + d["banco2"]
    sales = d["gross_sales"]
    if variant.trial_balance_broken:
        bank = -125_000.0
        sales = r2(sales + 2_000_000)
    # (código, descripción, columna 3..6, valor)
    leaves = [
        ("1101", "Caja chica", 3, d["caja_chica"]),
        ("1102", "Bancos", 3, bank),
        ("1103", "Cuentas por cobrar", 3, d["cxc_total"]),
        ("1104", "Inventario materia prima", 3, d["inv_fin_mp"]),
        ("1105", "Inventario productos terminados", 3, d["inv_fin_pt"]),
        ("1106", "Gastos pagados por anticipado", 3, d["prepagados"]),
        ("1201", "Propiedad planta y equipo neto", 3, d["ppe_net"]),
        ("1301", "Otros activos", 3, d["otros_activos"]),
        ("2101", "Préstamos por pagar", 4, d["prestamos_total"]),
        ("2102", "Cuentas por pagar", 4, d["cxp_total"]),
        ("2103", "Acumulaciones y retenciones", 4, d["acum_total"]),
        ("2201", "Préstamos a largo plazo", 4, d["prest_lp"]),
        ("3101", "Capital social", 4, d["capital_social"]),
        ("3102", "Reserva legal", 4, d["reserva_legal"]),
        ("3103", "Beneficios acumulados", 4, d["retained"]),
        ("4101", "Ventas", 5, sales),
        ("4102", "Descuentos sobre ventas", 5, -d["discounts"]),
        ("5101", "Costo de ventas", 6, d["cogs"]),
        ("6101", "Compensaciones al personal", 6, d["comp_total"]),
        ("6102", "Gastos administrativos", 6, d["admin_total"]),
        ("6103", "Depreciación y amortización", 6, d["dep_amort"]),
        ("6104", "Gastos financieros", 6, d["fin_exp"]),
        ("6105", "Impuesto sobre la renta", 6, d["isr"]),
    ]
    parents = {
        "1": "ACTIVOS",
        "11": "ACTIVOS CORRIENTES",
        "12": "ACTIVOS FIJOS",
        "13": "OTROS ACTIVOS",
        "2": "PASIVOS",
        "21": "PASIVOS CORRIENTES",
        "22": "PASIVOS A LARGO PLAZO",
        "3": "CAPITAL",
        "31": "CAPITAL CONTABLE",
        "4": "INGRESOS",
        "41": "VENTAS",
        "5": "COSTOS",
        "51": "COSTO DE VENTAS",
        "6": "GASTOS",
        "61": "GASTOS DE OPERACION",
    }
    codes = sorted(set(parents) | {code for code, *_ in leaves})
    leaf_map = {code: (desc, col, value) for code, desc, col, value in leaves}
    row = 6
    for code in codes:
        ws.cell(row, 1, code)
        if code in leaf_map:
            desc, col, value = leaf_map[code]
            ws.cell(row, 2, desc)
            ws.cell(row, col, r2(value))
        else:
            ws.cell(row, 2, parents[code]).font = Font(bold=True)
            children = [c for c in leaf_map if c.startswith(code)]
            for col in range(3, 7):
                total = sum(leaf_map[c][2] for c in children if leaf_map[c][1] == col)
                if total:
                    ws.cell(row, col, r2(total))
        row += 1
    ws.cell(row, 2, "TOTALES").font = Font(bold=True)
    for col in "CDEF":
        ws[f"{col}{row}"] = "=" + "+".join(f"{col}{r}" for r in range(6, row) if str(ws.cell(r, 1).value) in leaf_map)
    ws.cell(row + 1, 2, "PRUEBA CERO")
    ws[f"C{row + 1}"] = f"=C{row}+F{row}-D{row}-E{row}"
    total_debit = sum(v for _, _, col, v in leaves if col in (3, 6))
    total_credit = sum(v for _, _, col, v in leaves if col in (4, 5))
    expected["trialBalance"] = {"year": year, "zeroTest": r2(total_debit - total_credit), "leafCount": len(leaves)}


def write_fiscal_sheets(wb) -> None:
    """Papeles de trabajo fiscales: deben quedar como hojas ignoradas."""
    specs = {
        "export": (
            "RELACION DE EXPORTACIONES",
            ["FECHA", "CLIENTE DEL EXTERIOR", "FACTURA", "MONTO US$", "TASA", "MONTO RD$"],
        ),
        "anticipos": ("ANTICIPOS DEL IMPUESTO SOBRE LA RENTA", ["MES", "FECHA DE PAGO", "BASE", "TASA", "ANTICIPO"]),
        "desc": ("NOTAS DE CREDITO EMITIDAS", ["FECHA", "NCF", "RNC", "CLIENTE", "MONTO"]),
        "CXP": ("RELACION DE CUENTAS POR PAGAR A PROVEEDORES", ["RNC", "PROVEEDOR", "NCF", "FECHA", "MONTO"]),
        "IMPORT": (
            "RELACION DE IMPORTACIONES",
            ["FECHA", "PROVEEDOR", "FOB", "FLETE", "SEGURO", "CIF", "GRAVAMEN", "ITBIS"],
        ),
        "PRESTLAB": (
            "PRESTACIONES LABORALES PAGADAS",
            ["NOMBRE", "CEDULA", "FECHA ENTRADA", "FECHA SALIDA", "PREAVISO", "CESANTIA", "TOTAL"],
        ),
        "ADICIONES": ("ADICIONES DE ACTIVOS FIJOS", ["FECHA", "DESCRIPCION", "CATEGORIA", "MONTO"]),
        "ANEXOA1": ("ANEXO A1 - DECLARACION JURADA IR-2", ["CASILLA", "CONCEPTO", "MONTO"]),
        "ANEXOB1": ("ANEXO B1 - DECLARACION JURADA IR-2", ["CASILLA", "CONCEPTO", "MONTO"]),
    }
    for name, (title, headers) in specs.items():
        ws = wb.create_sheet(name)
        ws.cell(1, 1, COMPANY).font = Font(bold=True)
        ws.cell(2, 1, title).font = Font(bold=True)
        ws.cell(3, 1, "RNC: 000-00000-0")
        for i, h in enumerate(headers, start=1):
            ws.cell(5, i, h).font = Font(bold=True)
        for r in range(6, 10):
            for i, h in enumerate(headers, start=1):
                if h in ("RNC",):
                    ws.cell(r, i, "000-00000-0")
                elif h in ("NOMBRE", "PROVEEDOR", "CLIENTE", "CLIENTE DEL EXTERIOR"):
                    ws.cell(r, i, f"{h.split()[0]} FICTICIO {r - 5}")
                elif h == "CEDULA":
                    ws.cell(r, i, "000-0000000-0")
                elif h in ("MONTO", "FOB", "CIF", "TOTAL", "ANTICIPO", "MONTO RD$", "PREAVISO", "CESANTIA"):
                    ws.cell(r, i, 10_000 * (r - 4))
                else:
                    ws.cell(r, i, f"Dato {r - 5}")
    wb.create_sheet("BONIF.")  # vacía


# ---------------------------------------------------------------------------------------------
# Construcción de variantes
# ---------------------------------------------------------------------------------------------
def build_workbook(variant: Variant) -> tuple[Workbook, dict]:
    years_desc = sorted(variant.years, reverse=True)  # año actual primero, como en los libros reales
    # Toda variante usa la misma serie 2022–2024, así los años coinciden entre archivos.
    data = derive(sorted(INPUTS))
    wb = Workbook()
    wb.remove(wb.active)
    expected: dict = {"rows": {}}
    write_balance(wb, data, years_desc, variant, expected)
    write_income(wb, data, years_desc, variant, expected)
    write_equity(wb, data, years_desc, variant)
    write_notes(wb, data, years_desc, variant, expected)
    write_roster(wb, years_desc[0], expected)
    write_monthly(wb, data, years_desc[0], expected)
    write_trial_balance(wb, data, years_desc[0], variant, expected)
    write_fiscal_sheets(wb)
    expected["years"] = sorted(variant.years) if not variant.income_single_year else sorted(variant.years)
    expected["values"] = {str(y): {k: data[y][k] for k in sorted(data[y])} for y in sorted(variant.years)}
    return wb, expected


def convert_with_libreoffice(source: Path, target_ext: str, out_dir: Path) -> Path:
    """Recalcula las fórmulas (valores cacheados) y convierte al formato pedido."""
    filters = {"xls": "xls:MS Excel 97", "xlsx": "xlsx:Calc MS Excel 2007 XML"}
    with tempfile.TemporaryDirectory() as tmp:
        subprocess.run(
            [
                "soffice",
                "--headless",
                "--norestore",
                "--calc",
                "--convert-to",
                filters[target_ext],
                "--outdir",
                tmp,
                str(source),
            ],
            check=True,
            capture_output=True,
            timeout=180,
        )
        produced = Path(tmp) / f"{source.stem}.{target_ext}"
        if not produced.exists():
            raise RuntimeError(f"LibreOffice no generó {produced.name}")
        destination = out_dir / f"{source.stem}.{target_ext}"
        shutil.move(produced, destination)
        return destination


VARIANTS = [
    Variant("libro_sintetico_2a", [2023, 2024], "Libro válido de 2 años (caso principal).", ext=("xls", "xlsx")),
    Variant(
        "libro_sintetico_3a",
        [2022, 2023, 2024],
        "Libro de 3 años con márgenes que se deterioran (para el diagnóstico).",
        ext=("xls", "xlsx"),
    ),
    Variant(
        "inc1_balance_no_cuadra",
        [2023, 2024],
        "Total pasivos & capital del año actual 50.000 por encima del total de activos (0,04 %, fuera de la tolerancia de 0,01 %).",
        balance_mismatch=50_000,
        expected_warnings=["quality.balance_mismatch"],
    ),
    Variant(
        "inc2_nota_no_cuadra",
        [2023, 2024],
        "La nota 4 (cuentas por cobrar) suma 75.000 más que la línea del balance.",
        note_mismatch=True,
        expected_warnings=["quality.note_mismatch"],
    ),
    Variant(
        "inc3_patrimonio",
        [2023, 2024],
        "El estado de cambios en el patrimonio no refleja la ganancia del año actual.",
        equity_mismatch=True,
        expected_warnings=["quality.equity_mismatch"],
    ),
    Variant(
        "inc4_comprobacion",
        [2023, 2024],
        "El balance de comprobación no cuadra, tiene bancos negativos y sus ventas difieren del estado de resultados.",
        trial_balance_broken=True,
        expected_warnings=[
            "quality.trial_balance_unbalanced",
            "quality.trial_balance_negative_balance",
            "quality.trial_balance_sales_mismatch",
        ],
    ),
    Variant(
        "inc5_nombre_empresa",
        [2023, 2024],
        "La hoja nota3 trae el nombre de la empresa escrito distinto.",
        company_name_mismatch=True,
        expected_warnings=["quality.company_name_mismatch"],
    ),
    Variant(
        "inc_subtotal",
        [2023, 2024],
        "El total de gastos de operaciones no coincide con la suma de sus líneas.",
        subtotal_mismatch=True,
        expected_warnings=["quality.subtotal_mismatch"],
    ),
    Variant(
        "inc_anio_faltante",
        [2023, 2024],
        "El estado de resultados solo trae el año actual.",
        income_single_year=True,
        expected_warnings=["quality.missing_year"],
    ),
    Variant(
        "invalido_anios",
        [2023, 2024],
        "Las filas de años traen valores fuera de rango (1849/1850).",
        ext=("xlsx",),
        invalid_years=True,
    ),
]


def write_invalid_files(out_dir: Path, readme: list[str], manifest: dict) -> None:
    # Solo hojas fiscales: sin balance ni estado de resultados.
    wb = Workbook()
    wb.remove(wb.active)
    write_fiscal_sheets(wb)
    path = out_dir / "invalido_sin_estados.xlsx"
    wb.save(path)
    manifest["invalido_sin_estados.xlsx"] = {"blockingError": "upload.no_financial_statements"}
    readme.append(
        "| `invalido_sin_estados.xlsx` | Solo hojas fiscales: falta el balance y el estado de resultados → error bloqueante. |"
    )

    wb = Workbook()
    path = out_dir / "invalido_vacio.xlsx"
    wb.save(path)
    manifest["invalido_vacio.xlsx"] = {"blockingError": "upload.empty_file"}
    readme.append("| `invalido_vacio.xlsx` | Libro sin datos → error bloqueante. |")

    (out_dir / "invalido_extension_falsa.xls").write_text("Esto no es un libro de Excel.\n", encoding="utf-8")
    manifest["invalido_extension_falsa.xls"] = {"blockingError": "upload.invalid_signature"}
    readme.append("| `invalido_extension_falsa.xls` | Texto con extensión `.xls` → firma inválida. |")

    (out_dir / "invalido_corrupto.xlsx").write_bytes(b"PK\x03\x04" + bytes(range(256)) * 8)
    manifest["invalido_corrupto.xlsx"] = {"blockingError": "upload.unreadable"}
    readme.append("| `invalido_corrupto.xlsx` | Firma ZIP pero contenido dañado → ilegible. |")

    (out_dir / "invalido_corrupto.xls").write_bytes(bytes.fromhex("D0CF11E0A1B11AE1") + bytes(range(256)) * 8)
    manifest["invalido_corrupto.xls"] = {"blockingError": "upload.unreadable"}
    readme.append("| `invalido_corrupto.xls` | Firma OLE2 pero contenido dañado → ilegible. |")


def write_simple_templates(out_dir: Path, readme: list[str], manifest: dict) -> None:
    data = derive(sorted(INPUTS))
    lines = [
        ("Ventas netas", "gross_sales"),
        ("Descuentos sobre ventas", "discounts"),
        ("Costo de ventas", "cogs"),
        ("Compensaciones al personal", "comp_total"),
        ("Gastos administrativos", "admin_total"),
        ("Depreciación & Amortización", "dep_amort"),
        ("Gastos financieros", "fin_exp"),
        ("Impuesto sobre la renta", "isr"),
        ("Ingresos netos", "net_income"),
    ]
    csv_lines = ["Cuenta,2023,2024"]
    for label, key in lines:
        csv_lines.append(f'"{label}",{data[2023][key]:.2f},{data[2024][key]:.2f}')
    (out_dir / "plantilla_estado_resultados.csv").write_text("\n".join(csv_lines) + "\n", encoding="utf-8")
    manifest["plantilla_estado_resultados.csv"] = {
        "fileType": "IncomeStatement",
        "years": [2023, 2024],
        "values": {label: {"2023": data[2023][key], "2024": data[2024][key]} for label, key in lines},
    }
    readme.append("| `plantilla_estado_resultados.csv` | Plantilla simple CSV de estado de resultados (2 años). |")

    wb = Workbook()
    ws = wb.active
    ws.title = "Datos operacionales"
    ws["A1"] = "OPSYNEX AI - PLANTILLA DE DATOS OPERACIONALES"
    ws["A3"], ws["B3"], ws["C3"] = "Concepto", 2023, 2024
    operational = [
        ("Unidades producidas", 1_180_000, 1_240_000),
        ("Cantidad de empleados", 14, 15),
        ("Consumo energético (kWh)", 2_350_000, 2_610_000),
        ("Scrap estimado", 1_450_000, 1_720_000),
        ("Costos operacionales", 26_300_000, 28_900_000),
        ("Costo de energía", 9_850_000, 11_210_000),
    ]
    for i, (label, a, b) in enumerate(operational, start=4):
        ws.cell(i, 1, label)
        ws.cell(i, 2, a)
        ws.cell(i, 3, b)
    wb.save(out_dir / "plantilla_operacional.xlsx")
    manifest["plantilla_operacional.xlsx"] = {
        "fileType": "Operational",
        "years": [2023, 2024],
        "values": {label: {"2023": a, "2024": b} for label, a, b in operational},
    }
    readme.append("| `plantilla_operacional.xlsx` | Plantilla simple de datos operacionales (2 años). |")


def main() -> int:
    if shutil.which("soffice") is None:
        print("Falta LibreOffice (soffice) para calcular las fórmulas y generar .xls", file=sys.stderr)
        return 1
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for old in OUT_DIR.glob("*"):
        if old.suffix in (".xls", ".xlsx", ".csv", ".json"):
            old.unlink()

    manifest: dict = {}
    readme = [
        "| Archivo | Descripción |",
        "|---|---|",
    ]
    with tempfile.TemporaryDirectory() as tmp:
        for variant in VARIANTS:
            wb, expected = build_workbook(variant)
            source = Path(tmp) / f"{variant.name}.xlsx"
            wb.save(source)
            for ext in variant.ext:
                produced = convert_with_libreoffice(source, ext, OUT_DIR)
                entry = copy.deepcopy(expected)
                entry["description"] = variant.description
                entry["expectedWarnings"] = variant.expected_warnings
                if variant.invalid_years:
                    entry["blockingError"] = "upload.years_not_found"
                manifest[produced.name] = entry
                readme.append(f"| `{produced.name}` | {variant.description} |")
    write_invalid_files(OUT_DIR, readme, manifest)
    write_simple_templates(OUT_DIR, readme, manifest)

    (OUT_DIR / "expected_values.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False, sort_keys=True) + "\n", encoding="utf-8"
    )
    write_readme(readme, manifest)
    print(f"Generados {len(manifest)} archivos en {OUT_DIR.relative_to(ROOT)}")
    return 0


def write_readme(table: list[str], manifest: dict) -> None:
    base = manifest["libro_sintetico_2a.xls"]["values"]

    def money(v: float) -> str:
        return f"{v:,.2f}"

    keys = [
        ("Ventas netas (GrossSales)", "gross_sales"),
        ("Descuentos sobre ventas", "discounts"),
        ("Revenue (ventas − descuentos)", "net_sales"),
        ("Costo de ventas", "cogs"),
        ("Compensaciones al personal", "comp_total"),
        ("Gastos administrativos", "admin_total"),
        ("Depreciación & amortización (gastos)", "dep_amort"),
        ("Depreciación en el costo", "fab_dep"),
        ("Gastos financieros", "fin_exp"),
        ("Ganancia antes de ISR", "ebt"),
        ("ISR (27 %)", "isr"),
        ("Utilidad neta", "net_income"),
        ("EBITDA", "ebitda"),
        ("Efectivo", "cash"),
        ("Cuentas por cobrar", "cxc_total"),
        ("Inventarios", "inventory"),
        ("Total corriente (activos)", "current_assets"),
        ("Total activos", "total_assets"),
        ("Total corriente (pasivos)", "pasivo_corriente"),
        ("Deuda financiera", "financial_debt"),
        ("Total pasivos", "total_liabilities"),
        ("Total capital", "equity"),
        ("Energía (nota 3 + nota 4)", None),
    ]
    rows = ["| Concepto | 2023 | 2024 |", "|---|---:|---:|"]
    for label, key in keys:
        if key is None:
            a = base["2023"]["g_energia"] + base["2023"]["fab_energia"]
            b = base["2024"]["g_energia"] + base["2024"]["fab_energia"]
        else:
            a, b = base["2023"][key], base["2024"][key]
        rows.append(f"| {label} | {money(a)} | {money(b)} |")
    employees = manifest["libro_sintetico_2a.xls"]["employees"]
    text = f"""# Datos de prueba sintéticos

Generados por `backend/tests/TestData/generate_sample_workbooks.py` (no editar a mano):

```bash
python3 backend/tests/TestData/generate_sample_workbooks.py
```

Reproducen la **estructura** de los libros contables de los clientes
(`docs/especificaciones/FORMATO_EXCEL_ENTRADA.md`) con una empresa ficticia
(«{COMPANY}») y **valores inventados**. No contienen datos reales: los empleados son
"EMPLEADO 001…", las cédulas "000-0000000-0" y los RNC "000-00000-0". Las fórmulas tienen sus
valores calculados por LibreOffice, como en un archivo guardado por Excel.

## Archivos

{chr(10).join(table)}

## Valores esperados del libro de 2 años (RD$)

{chr(10).join(rows)}

Nómina (`REGPASCUAL`, {manifest["libro_sintetico_2a.xls"]["years"][-1]}): **{employees["count"]} empleados**
({", ".join(f"{k.title()}: {v['count']}" for k, v in employees["byDepartment"].items())}); nómina anual
total RD$ {money(employees["payroll"])}.

`EBITDA = Ganancia antes de ISR + Gastos financieros + D&A en gastos + Depreciación en el costo`.

Todos los valores, por archivo, año y concepto, están en `expected_values.json`, que leen las
pruebas automáticas.
"""
    (OUT_DIR / "README.md").write_text(text, encoding="utf-8")


if __name__ == "__main__":
    sys.exit(main())
