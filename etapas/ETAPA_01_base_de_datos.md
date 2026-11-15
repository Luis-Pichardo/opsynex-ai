# Etapa 01 — Base de datos y modelos

**Depende de:** 00 · **Rama:** `etapa-01-base-de-datos`

## Prompt

> Implementa el modelo de datos de Opsynex AI con SQLAlchemy 2.x y migraciones Alembic sobre
> PostgreSQL, basándote en la sección 16 de `ESPECIFICACIONES_DEL_SISTEMA` y agregando lo
> necesario para multi-empresa, cargas de archivos y homologación. Incluye repositorios básicos
> y tests. El modelo industrial (producción por máquina) NO va en esta etapa (Etapa 11).

## Tareas

1. Configura Alembic (`backend/alembic/`) leyendo `DATABASE_URL` de la configuración.
2. Modelos (todas las tablas con `id` UUID o entero, `created_at`, `updated_at`):
   - `companies`: name, industry (`manufacturing` | `services`), sector, currency (default `USD`).
   - `users`: email (único), password_hash, full_name, role (`admin` | `analyst` | `client`),
     company_id (nullable para admin global), is_active.
   - `uploads`: company_id, user_id, filename, file_type (`income_statement`, `balance_sheet`,
     `cash_flow`, `cost_structure`, `operational`), status (`pending`, `validated`, `failed`,
     `processed`), errors (JSON), rows_count.
   - `account_mappings`: company_id, client_account (texto original), opsynex_category, created_by.
   - `financial_data`: company_id, year, revenue, cogs, operating_expenses, ebitda, total_cost,
     overhead_cost, energy_cost, maintenance_cost, material_loss, net_income, total_assets,
     total_liabilities, equity, operating_cash_flow, upload_id. Único (company_id, year).
   - `operational_data`: company_id, year, units_produced, employee_count, scrap_estimate,
     energy_cost, operational_costs, energy_consumption_kwh, upload_id. Único (company_id, year).
   - `kpi_results`: company_id, year, ebitda_margin, gross_margin, cost_efficiency,
     revenue_per_employee, operating_cost_ratio, calculated_at. Único (company_id, year).
   - `problems`: company_id, year, code (id de la regla), title, description, category,
     severity (`high`|`medium`|`low`), impact_category (`margin_loss`, `excess_cost`,
     `productivity_loss`, `operational_waste`), impact_value, recommendation, evidence (JSON),
     status (`open`, `acknowledged`, `resolved`).
   - `scores`: company_id, year, total_score, profitability_score, cost_efficiency_score,
     productivity_score, financial_stability_score, interpretation.
3. Catálogo de categorías estándar Opsynex en `app/core/categories.py` (enum + descripción):
   `revenue`, `cogs`, `operating_expenses`, `overhead_cost`, `energy_cost`, `material_loss`,
   `maintenance_cost`, `labor_cost`, `depreciation`, `interest`, `taxes`, `other`, etc.
4. Repositorios base con operaciones CRUD y filtros por `company_id`.
5. Migración inicial generada y aplicada.
6. Fixtures de tests (`tests/conftest.py`) con BD de prueba aislada por test.

## Criterios de aceptación

- [ ] `alembic upgrade head` crea todas las tablas en PostgreSQL; `alembic downgrade base` las elimina.
- [ ] Tests de repositorios (crear/leer/filtrar por empresa, restricciones únicas) pasan.
- [ ] Diagrama ER en texto (Mermaid) incluido en la documentación.

## Documentación a actualizar

- `03_modelo_datos.md`: cada tabla con columnas, tipos y descripción; diagrama ER Mermaid;
  catálogo de categorías.
- `12_bitacora.md`: entrada de la Etapa 01.
