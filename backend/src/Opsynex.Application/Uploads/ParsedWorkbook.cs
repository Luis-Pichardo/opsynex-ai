using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Uploads;

/// <summary>Resultado de leer, clasificar, extraer y validar un archivo cargado.</summary>
public sealed class ParsedWorkbook
{
    public UploadFileType FileType { get; set; } = UploadFileType.AccountingWorkbook;

    public List<UploadSheet> Sheets { get; } = [];

    /// <summary>Filas extraídas de los estados, notas, balance de comprobación y plantillas.</summary>
    public List<ParsedLine> Lines { get; } = [];

    /// <summary>Años detectados en los estados financieros, ascendentes.</summary>
    public List<int> Years { get; } = [];

    public List<UploadIssue> Errors { get; } = [];

    public List<UploadIssue> Warnings { get; } = [];

    public EmployeeSummary? Employees { get; set; }

    public List<MonthlyValue> Monthly { get; } = [];

    public TrialBalanceResult? TrialBalance { get; set; }

    /// <summary>Datos operacionales (plantilla operacional y cantidad de empleados de la nómina).</summary>
    public List<OperationalValue> Operational { get; } = [];

    /// <summary>Tablas industriales (producción, costos y estándares), si el archivo es del módulo industrial.</summary>
    public IndustrialTables? Industrial { get; set; }

    public bool HasBlockingErrors => Errors.Count > 0;
}

/// <summary>Registros leídos de un archivo industrial (ya validados fila por fila).</summary>
public sealed class IndustrialTables
{
    public List<Domain.Engines.Industrial.ProductionRecord> Production { get; } = [];

    public List<Domain.Engines.Industrial.CostRecord> Costs { get; } = [];

    public List<Domain.Engines.Industrial.StandardRecord> Standards { get; } = [];

    public int Count => Production.Count + Costs.Count + Standards.Count;

    public IEnumerable<string> Machines =>
        Production.Select(p => p.MachineId).Concat(Costs.Select(c => c.MachineId)).Concat(Standards.Select(s => s.MachineId))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<DateOnly> Dates => Production.Select(p => p.Date).Concat(Costs.Select(c => c.Date));
}

/// <summary>Fila de un estado financiero con sus valores por año.</summary>
public sealed record ParsedLine(
    string SheetName,
    SheetType SheetType,
    int RowNumber,
    string? Section,
    string OriginalLabel,
    string NormalizedLabel,
    string? AccountCode,
    RowKind RowKind,
    IReadOnlyDictionary<int, decimal> Values);

/// <summary>Conteo de empleados de la nómina. Nunca incluye nombres, cédulas ni sueldos individuales.</summary>
public sealed record EmployeeSummary(int Year, int Count, IReadOnlyList<DepartmentSummary> ByDepartment, decimal Payroll);

/// <summary>Ventas y compras de un mes.</summary>
public sealed record MonthlyValue(int Year, int Month, decimal? Revenue, decimal? Purchases);

/// <summary>Campo de <c>OperationalData</c>.</summary>
public enum OperationalField
{
    UnitsProduced,
    EmployeeCount,
    EnergyConsumptionKwh,
    ScrapEstimate,
    OperationalCosts,
    EnergyCost,
}

/// <summary>Valor operacional de un año.</summary>
public sealed record OperationalValue(int Year, OperationalField Field, decimal Value);

/// <summary>Resultado de validar el balance de comprobación.</summary>
public sealed record TrialBalanceResult(int Year, bool Reliable, decimal Difference, int Accounts);
