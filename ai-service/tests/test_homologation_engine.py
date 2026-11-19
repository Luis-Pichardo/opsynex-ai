"""Motor de homologación: ejemplos del PDF, libro dominicano sintético y contexto."""

import pytest

from app.engines.homologation import AccountInput, KnownMapping, suggest
from app.engines.homologation.categories import Category
from app.engines.homologation.matcher import suggest_one
from app.engines.homologation.normalize import normalize_label

C = Category


def one(
    label: str, sheet_type: str | None = None, section: str | None = None, code: str | None = None
):
    return suggest_one(
        AccountInput(
            id=None, label=label, sheet_type=sheet_type, section=section, account_code=code
        )
    )


@pytest.mark.parametrize(
    ("label", "expected"),
    [
        ("Gasto Energía Planta", C.ENERGY_COST),
        ("Nómina Indirecta", C.OVERHEAD_COST),
        ("Desperdicio Materia Prima", C.MATERIAL_LOSS),
        ("Gasto Mantenimiento", C.MAINTENANCE_COST),
    ],
)
def test_pdf_examples(label: str, expected: C) -> None:
    result = one(label)
    assert result.category == expected
    assert result.confidence >= 0.8


def test_normalize_label() -> None:
    assert normalize_label("Menos: Cuentas por Cobrar (nota 4)") == "cuentas por cobrar"
    assert (
        normalize_label("Reparaciones & Mantenimiento Vehículos")
        == "reparaciones y mantenimiento vehiculos"
    )
    assert (
        normalize_label("Ingresos netos antes impuesto s/renta")
        == "ingresos netos antes impuesto s renta"
    )
    assert normalize_label("Banco Ficticio, Cta. *** 1101") == "banco ficticio cta"


# Líneas de detalle del libro sintético con las etiquetas de la sección 4 de la especificación
# (balance, estado de resultados y notas 3 y 4) y su categoría correcta.
SYNTHETIC_LINES = [
    ("BalanceSheet", "ACTIVOS CORRIENTES", "Efectivo en Cajas & Bancos", C.CASH),
    ("BalanceSheet", "ACTIVOS CORRIENTES", "Cuentas por Cobrar", C.ACCOUNTS_RECEIVABLE),
    ("BalanceSheet", "ACTIVOS CORRIENTES", "Inventario Productos Terminados", C.INVENTORY),
    ("BalanceSheet", "ACTIVOS CORRIENTES", "Inventario Materia Prima", C.INVENTORY),
    ("BalanceSheet", "ACTIVOS CORRIENTES", "Gastos pagados por anticipado", C.OTHER),
    ("BalanceSheet", "ACTIVOS CORRIENTES", "Total corriente", C.CURRENT_ASSETS),
    ("BalanceSheet", "PROPIEDAD PLANTA & EQUIPOS", "Propiedad, planta y equipo, neto", C.OTHER),
    ("BalanceSheet", "OTROS ACTIVOS", "Otros activos", C.OTHER),
    ("BalanceSheet", "OTROS ACTIVOS", "Total Activos", C.TOTAL_ASSETS),
    ("BalanceSheet", "PASIVO CORRIENTE", "Préstamos por Pagar", C.FINANCIAL_DEBT),
    ("BalanceSheet", "PASIVO CORRIENTE", "Cuentas por Pagar", C.ACCOUNTS_PAYABLE),
    ("BalanceSheet", "PASIVO CORRIENTE", "Acumulaciones y retenciones por pagar", C.OTHER),
    ("BalanceSheet", "PASIVO CORRIENTE", "Total corriente", C.CURRENT_LIABILITIES),
    ("BalanceSheet", "PASIVO A LARGO PLAZO", "Préstamos a largo plazo", C.FINANCIAL_DEBT),
    ("BalanceSheet", "CAPITAL", "Capital social", C.EQUITY),
    ("BalanceSheet", "CAPITAL", "Reserva legal", C.EQUITY),
    ("BalanceSheet", "CAPITAL", "Beneficios acumulados", C.EQUITY),
    ("BalanceSheet", "CAPITAL", "Beneficio del período", C.EQUITY),
    ("BalanceSheet", "CAPITAL", "Total capital", C.EQUITY),
    ("BalanceSheet", "CAPITAL", "Total Pasivos & Capital", C.TOTAL_LIABILITIES_AND_EQUITY),
    ("IncomeStatement", None, "Ventas netas", C.GROSS_SALES),
    ("IncomeStatement", None, "Descuentos sobre ventas", C.SALES_DISCOUNTS),
    ("IncomeStatement", None, "(subtotal sin etiqueta)", C.OTHER),
    ("IncomeStatement", None, "Costos de Ventas", C.COGS),
    ("IncomeStatement", None, "Ganancia bruta", C.OTHER),
    ("IncomeStatement", None, "Compensaciones al personal", C.OPERATING_EXPENSES),
    ("IncomeStatement", None, "Gastos Administrativos", C.OPERATING_EXPENSES),
    ("IncomeStatement", None, "Depreciación & Amortización", C.DEPRECIATION),
    ("IncomeStatement", None, "Total gastos de operaciones", C.OPERATING_EXPENSES),
    ("IncomeStatement", None, "Ganancia en operaciones", C.OTHER),
    ("IncomeStatement", None, "Gastos Financieros", C.FINANCIAL_EXPENSES),
    ("IncomeStatement", None, "Ingresos netos antes impuesto s/renta", C.EARNINGS_BEFORE_TAX),
    ("IncomeStatement", None, "Impuesto sobre la renta", C.INCOME_TAX),
    ("IncomeStatement", None, "Ingresos Netos", C.NET_INCOME),
    ("Notes", "11. COMPENSACIONES AL PERSONAL", "Sueldos", C.LABOR_COST),
    ("Notes", "11. COMPENSACIONES AL PERSONAL", "Regalía Pascual", C.LABOR_COST),
    ("Notes", "11. COMPENSACIONES AL PERSONAL", "Bonificaciones", C.LABOR_COST),
    ("Notes", "11. COMPENSACIONES AL PERSONAL", "Seguridad social (TSS)", C.LABOR_COST),
    ("Notes", "11. COMPENSACIONES AL PERSONAL", "Infotep", C.LABOR_COST),
    ("Notes", "11. COMPENSACIONES AL PERSONAL", "Preaviso & Cesantía", C.LABOR_COST),
    ("Notes", "11. COMPENSACIONES AL PERSONAL", "Total compensaciones al personal", C.LABOR_COST),
    (
        "Notes",
        "13. GASTOS GENERALES Y ADMINISTRATIVOS",
        "Honorarios profesionales",
        C.OVERHEAD_COST,
    ),
    ("Notes", "13. GASTOS GENERALES Y ADMINISTRATIVOS", "Energía Eléctrica", C.ENERGY_COST),
    ("Notes", "13. GASTOS GENERALES Y ADMINISTRATIVOS", "Combustibles", C.OVERHEAD_COST),
    ("Notes", "13. GASTOS GENERALES Y ADMINISTRATIVOS", "Reparaciones", C.MAINTENANCE_COST),
    ("Notes", "13. GASTOS GENERALES Y ADMINISTRATIVOS", "Mantenimientos", C.MAINTENANCE_COST),
    (
        "Notes",
        "13. GASTOS GENERALES Y ADMINISTRATIVOS",
        "Reparaciones & Mantenimiento Vehículos",
        C.MAINTENANCE_COST,
    ),
    ("Notes", "13. GASTOS GENERALES Y ADMINISTRATIVOS", "Seguros", C.OVERHEAD_COST),
    ("Notes", "13. GASTOS GENERALES Y ADMINISTRATIVOS", "Teléfonos e internet", C.OVERHEAD_COST),
    ("Notes", "13. GASTOS GENERALES Y ADMINISTRATIVOS", "Alquileres", C.OVERHEAD_COST),
    ("Notes", "13. GASTOS GENERALES Y ADMINISTRATIVOS", "Otros gastos", C.OVERHEAD_COST),
    (
        "Notes",
        "13. GASTOS GENERALES Y ADMINISTRATIVOS",
        "Total gastos generales y administrativos",
        C.OVERHEAD_COST,
    ),
    ("Notes", "12. COSTO DE PRODUCCION Y VENTAS", "Inventario inicial materia prima", C.INVENTORY),
    ("Notes", "12. COSTO DE PRODUCCION Y VENTAS", "Materias primas importadas", C.RAW_MATERIALS),
    (
        "Notes",
        "12. COSTO DE PRODUCCION Y VENTAS",
        "Compras de materias primas locales",
        C.RAW_MATERIALS,
    ),
    ("Notes", "12. COSTO DE PRODUCCION Y VENTAS", "Inventario final materia prima", C.INVENTORY),
    ("Notes", "12. COSTO DE PRODUCCION Y VENTAS", "Total materia prima utilizada", C.COGS),
    ("Notes", "12. COSTO DE PRODUCCION Y VENTAS", "Sueldos producción", C.LABOR_COST),
    ("Notes", "12. COSTO DE PRODUCCION Y VENTAS", "Regalía Pascual producción", C.LABOR_COST),
    (
        "Notes",
        "12. COSTO DE PRODUCCION Y VENTAS",
        "Seguridad social (TSS) producción",
        C.LABOR_COST,
    ),
    ("Notes", "12. COSTO DE PRODUCCION Y VENTAS", "Infotep producción", C.LABOR_COST),
    ("Notes", "12. COSTO DE PRODUCCION Y VENTAS", "Total labor directa", C.LABOR_COST),
    ("Notes", "12. COSTO DE PRODUCCION Y VENTAS", "Energía Eléctrica", C.ENERGY_COST),
    (
        "Notes",
        "12. COSTO DE PRODUCCION Y VENTAS",
        "Reparaciones & mantenimientos",
        C.MAINTENANCE_COST,
    ),
    ("Notes", "12. COSTO DE PRODUCCION Y VENTAS", "Combustibles y lubricantes", C.OVERHEAD_COST),
    (
        "Notes",
        "12. COSTO DE PRODUCCION Y VENTAS",
        "Depreciación Maquinarias & Equipos",
        C.DEPRECIATION,
    ),
    ("Notes", "12. COSTO DE PRODUCCION Y VENTAS", "Total gastos de fabricación", C.COGS),
    ("Notes", "12. COSTO DE PRODUCCION Y VENTAS", "Total costo de producción", C.COGS),
    (
        "Notes",
        "12. COSTO DE PRODUCCION Y VENTAS",
        "Inventario inicial productos terminados",
        C.INVENTORY,
    ),
    (
        "Notes",
        "12. COSTO DE PRODUCCION Y VENTAS",
        "Inventario final productos terminados",
        C.INVENTORY,
    ),
    ("Notes", "12. COSTO DE PRODUCCION Y VENTAS", "Costo de ventas", C.COGS),
    # Notas que desglosan el balance: nunca deben caer en categorías analíticas.
    ("Notes", "5. PROPIEDAD, PLANTA Y EQUIPOS", "Depreciación acumulada", C.OTHER),
    ("Notes", "5. PROPIEDAD, PLANTA Y EQUIPOS", "Maquinarias & equipos", C.OTHER),
    ("Notes", "4. CUENTAS POR COBRAR", "Clientes", C.ACCOUNTS_RECEIVABLE),
    ("Notes", "9. ACUMULACIONES Y RETENCIONES POR PAGAR", "TSS por pagar", C.OTHER),
]


@pytest.mark.parametrize(("sheet", "section", "label", "expected"), SYNTHETIC_LINES)
def test_synthetic_workbook_lines(sheet: str, section: str | None, label: str, expected: C) -> None:
    result = one(label, sheet, section)
    assert result.category == expected, f"{label}: {result.category} ({result.method})"
    assert result.confidence >= 0.8


def test_synthetic_workbook_accuracy_is_at_least_90_percent() -> None:
    results = suggest(
        [
            AccountInput(None, label, None, sheet, section)
            for sheet, section, label, _ in SYNTHETIC_LINES
        ]
    )
    correct = sum(
        1
        for (_, _, _, expected), result in zip(SYNTHETIC_LINES, results, strict=True)
        if result.category == expected and result.confidence >= 0.8
    )
    assert correct / len(SYNTHETIC_LINES) >= 0.9


def test_total_corriente_depends_on_section() -> None:
    assert one("Total corriente", "BalanceSheet", "ACTIVOS CORRIENTES").category == C.CURRENT_ASSETS
    assert (
        one("Total corriente", "BalanceSheet", "PASIVO CORRIENTE").category == C.CURRENT_LIABILITIES
    )


def test_same_label_depends_on_sheet_type() -> None:
    assert one("Gastos Administrativos", "IncomeStatement").category == C.OPERATING_EXPENSES
    assert (
        one("Gastos Administrativos", "Notes", "13. GASTOS GENERALES").category == C.OVERHEAD_COST
    )
    assert one("Compensaciones al personal", "Notes", "11. COMPENSACIONES").category == C.LABOR_COST


def test_trial_balance_code_guides_category() -> None:
    assert one("Bancos", "TrialBalance", "Asset", "1102").category == C.CASH
    assert one("Ventas", "TrialBalance", "Income", "4101").category == C.GROSS_SALES
    assert (
        one("Gastos financieros", "TrialBalance", "CostExpense", "6104").category
        == C.FINANCIAL_EXPENSES
    )


def test_dominican_terms() -> None:
    for label in (
        "Regalía Pascual",
        "TSS",
        "Seguro Familiar de Salud",
        "Infotep",
        "Riesgos laborales",
        "Labor directa",
    ):
        assert one(label, "Notes").category == C.LABOR_COST, label
    assert one("Gastos de Fabricación", "Notes").category == C.COGS
    assert one("ISR", "IncomeStatement").category == C.INCOME_TAX


def test_fuzzy_match_tolerates_typos() -> None:
    result = one("Mantenimeinto de equipos")
    assert result.category == C.MAINTENANCE_COST
    assert result.method == "fuzzy"


def test_unknown_account_is_other_with_low_confidence() -> None:
    result = one("Partida especial ZQX", "IncomeStatement")
    assert result.category == C.OTHER
    assert result.method == "none"
    assert result.confidence < 0.8


def test_known_mapping_has_priority() -> None:
    known = [KnownMapping("Partida especial ZQX", C.OVERHEAD_COST, "IncomeStatement")]
    result = suggest(
        [AccountInput("a1", "Partida especial ZQX", sheet_type="IncomeStatement")], known
    )[0]
    assert result.category == C.OVERHEAD_COST
    assert result.confidence == 1.0
    assert result.method == "known"
    assert result.id == "a1"


def test_alternatives_exclude_chosen_category() -> None:
    result = one("Gastos de mantenimiento y energía")
    assert all(a.category != result.category for a in result.alternatives)
    assert len(result.alternatives) <= 3
