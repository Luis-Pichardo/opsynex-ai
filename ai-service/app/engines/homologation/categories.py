"""Categorías estándar de Opsynex (en snake_case, igual que el enum OpsynexCategory del API)."""

from enum import StrEnum


class Category(StrEnum):
    REVENUE = "revenue"
    COGS = "cogs"
    OPERATING_EXPENSES = "operating_expenses"
    OVERHEAD_COST = "overhead_cost"
    ENERGY_COST = "energy_cost"
    MATERIAL_LOSS = "material_loss"
    MAINTENANCE_COST = "maintenance_cost"
    LABOR_COST = "labor_cost"
    DEPRECIATION = "depreciation"
    INTEREST = "interest"
    TAXES = "taxes"
    NET_INCOME = "net_income"
    TOTAL_ASSETS = "total_assets"
    TOTAL_LIABILITIES = "total_liabilities"
    EQUITY = "equity"
    OPERATING_CASH_FLOW = "operating_cash_flow"
    OTHER = "other"
    GROSS_SALES = "gross_sales"
    SALES_DISCOUNTS = "sales_discounts"
    FINANCIAL_EXPENSES = "financial_expenses"
    EARNINGS_BEFORE_TAX = "earnings_before_tax"
    INCOME_TAX = "income_tax"
    RAW_MATERIALS = "raw_materials"
    CASH = "cash"
    ACCOUNTS_RECEIVABLE = "accounts_receivable"
    INVENTORY = "inventory"
    CURRENT_ASSETS = "current_assets"
    CURRENT_LIABILITIES = "current_liabilities"
    ACCOUNTS_PAYABLE = "accounts_payable"
    FINANCIAL_DEBT = "financial_debt"
    TOTAL_LIABILITIES_AND_EQUITY = "total_liabilities_and_equity"


C = Category

INCOME = frozenset(
    {
        C.REVENUE,
        C.COGS,
        C.OPERATING_EXPENSES,
        C.OVERHEAD_COST,
        C.ENERGY_COST,
        C.MATERIAL_LOSS,
        C.MAINTENANCE_COST,
        C.LABOR_COST,
        C.DEPRECIATION,
        C.INTEREST,
        C.TAXES,
        C.NET_INCOME,
        C.GROSS_SALES,
        C.SALES_DISCOUNTS,
        C.FINANCIAL_EXPENSES,
        C.EARNINGS_BEFORE_TAX,
        C.INCOME_TAX,
        C.RAW_MATERIALS,
    }
)
BALANCE = frozenset(
    {
        C.TOTAL_ASSETS,
        C.TOTAL_LIABILITIES,
        C.EQUITY,
        C.CASH,
        C.ACCOUNTS_RECEIVABLE,
        C.INVENTORY,
        C.CURRENT_ASSETS,
        C.CURRENT_LIABILITIES,
        C.ACCOUNTS_PAYABLE,
        C.FINANCIAL_DEBT,
        C.TOTAL_LIABILITIES_AND_EQUITY,
    }
)
CASH_FLOW = frozenset({C.OPERATING_CASH_FLOW, C.NET_INCOME, C.DEPRECIATION})
ALL = frozenset(Category)

# Primer dígito del código de cuenta (balance de comprobación) → categorías posibles.
ACCOUNT_CODE_GROUPS: dict[str, frozenset[Category]] = {
    "1": frozenset({C.CASH, C.ACCOUNTS_RECEIVABLE, C.INVENTORY, C.CURRENT_ASSETS, C.TOTAL_ASSETS}),
    "2": frozenset(
        {C.ACCOUNTS_PAYABLE, C.FINANCIAL_DEBT, C.CURRENT_LIABILITIES, C.TOTAL_LIABILITIES}
    ),
    "3": frozenset({C.EQUITY}),
    "4": frozenset({C.GROSS_SALES, C.SALES_DISCOUNTS, C.REVENUE}),
    "5": frozenset(
        {
            C.COGS,
            C.RAW_MATERIALS,
            C.LABOR_COST,
            C.ENERGY_COST,
            C.MAINTENANCE_COST,
            C.DEPRECIATION,
            C.OVERHEAD_COST,
            C.MATERIAL_LOSS,
        }
    ),
    "6": frozenset(
        {
            C.OPERATING_EXPENSES,
            C.OVERHEAD_COST,
            C.ENERGY_COST,
            C.MAINTENANCE_COST,
            C.LABOR_COST,
            C.DEPRECIATION,
            C.FINANCIAL_EXPENSES,
            C.INCOME_TAX,
            C.MATERIAL_LOSS,
        }
    ),
}
