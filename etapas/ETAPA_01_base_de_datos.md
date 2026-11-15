# Etapa 01 — Base de datos y modelos (EF Core + SQL Server)

**Depende de:** 00 · **Rama:** `etapa-01-base-de-datos`

## Prompt

> Implementa el modelo de datos de Opsynex AI con Entity Framework Core 10 sobre SQL Server,
> basado en la sección 16 de `ESPECIFICACIONES_DEL_SISTEMA` y ampliado para multi-empresa,
> cargas de archivos y homologación. Incluye migraciones, repositorios y tests. El modelo
> industrial (producción por máquina) **no** va aquí (etapa 12).

## Tareas

1. **Entidades** en `Opsynex.Domain/Entities`. Todas con `Id` (Guid), `CreatedAt` y `UpdatedAt`
   (llenados con un interceptor):
   - `Company`: Name, Industry (`Manufacturing` | `Services`), Sector, Currency (por defecto `USD`).
   - `User`: Email (único), PasswordHash, FullName, Role (`Admin` | `Analyst` | `Client`),
     CompanyId (null para un admin global), IsActive.
   - `Upload`: CompanyId, UserId, FileName, FileType (`IncomeStatement`, `BalanceSheet`,
     `CashFlow`, `CostStructure`, `Operational`), Status (`Pending`, `Validated`, `Failed`,
     `Processed`), Errors (JSON), RowsCount.
   - `RawAccountValue`: UploadId, CompanyId, ClientAccount, Year, Value.
   - `AccountMapping`: CompanyId, ClientAccount, OpsynexCategory, Confidence, Method,
     ConfirmedBy, ConfirmedAt. Único (CompanyId, ClientAccount).
   - `FinancialData`: CompanyId, Year, Revenue, Cogs, OperatingExpenses, Ebitda, TotalCost,
     OverheadCost, EnergyCost, MaintenanceCost, MaterialLoss, LaborCost, Depreciation,
     NetIncome, TotalAssets, TotalLiabilities, Equity, OperatingCashFlow. Único (CompanyId, Year).
   - `OperationalData`: CompanyId, Year, UnitsProduced, EmployeeCount, ScrapEstimate,
     EnergyCost, OperationalCosts, EnergyConsumptionKwh, UploadId. Único (CompanyId, Year).
   - `KpiResult`: CompanyId, Year, EbitdaMargin, GrossMargin, CostEfficiency,
     RevenuePerEmployee, OperatingCostRatio, CalculatedAt. Único (CompanyId, Year).
   - `Problem`: CompanyId, Year, Code, Title, Description, Category, Severity (`High` |
     `Medium` | `Low`), ImpactCategory (`MarginLoss`, `ExcessCost`, `ProductivityLoss`,
     `OperationalWaste`), ImpactValue, Recommendation, AiRecommendation (null hasta la etapa
     11), Evidence (JSON), Status (`Open`, `Acknowledged`, `Resolved`).
   - `Score`: CompanyId, Year, TotalScore, ProfitabilityScore, CostEfficiencyScore,
     ProductivityScore, FinancialStabilityScore, WeightsUsed (JSON), Interpretation.
2. **Catálogo de categorías estándar** `OpsynexCategory` (enum con descripción en español):
   Revenue, Cogs, OperatingExpenses, OverheadCost, EnergyCost, MaterialLoss, MaintenanceCost,
   LaborCost, Depreciation, Interest, Taxes, NetIncome, TotalAssets, TotalLiabilities, Equity,
   OperatingCashFlow, Other.
3. **Infraestructura**: `OpsynexDbContext` con configuraciones Fluent API (`IEntityTypeConfiguration`),
   precisión decimal, enums guardados como string, columnas JSON, índices por
   `CompanyId` + `Year` y un filtro global por empresa (se conecta al usuario en la etapa 2).
4. Migración inicial `InitialCreate` y, en Development, aplicación automática al arrancar.
5. Repositorios genéricos o específicos y un `IUnitOfWork` en Application.
6. Tests: unitarios de configuración (restricciones únicas y precisión) e integración contra
   SQL Server (Testcontainers) o SQLite in-memory, según lo que permita el entorno.

## Criterios de aceptación

- [ ] `dotnet ef database update` crea todas las tablas en SQL Server.
- [ ] Los tests de repositorios pasan: crear, leer, filtrar por empresa y validar restricciones únicas.
- [ ] Hay un diagrama ER en Mermaid en la documentación.

## Documentación a actualizar

- `03_modelo_datos.md`: cada tabla con columnas, tipos y descripción; diagrama ER Mermaid;
  catálogo de categorías.
- `13_bitacora.md`.
