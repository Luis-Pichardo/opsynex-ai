using Opsynex.Domain.Common;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Entities;

/// <summary>
/// Archivo Excel/CSV cargado por un usuario. El archivo original **no** se guarda (privacidad):
/// solo el reporte de la carga y los valores extraídos.
/// </summary>
public sealed class Upload : Entity, ICompanyScoped
{
    public Guid CompanyId { get; set; }

    public Guid UserId { get; set; }

    public required string FileName { get; set; }

    /// <summary>Tipo detectado: libro contable completo o una plantilla simple.</summary>
    public UploadFileType FileType { get; set; }

    public UploadStatus Status { get; set; } = UploadStatus.Pending;

    public long FileSizeBytes { get; set; }

    /// <summary>Errores bloqueantes (columna JSON).</summary>
    public IList<UploadIssue> Errors { get; set; } = [];

    /// <summary>Advertencias de calidad que no bloquean la carga (columna JSON).</summary>
    public IList<UploadIssue> Warnings { get; set; } = [];

    /// <summary>Hojas del libro con su tipo detectado (columna JSON).</summary>
    public IList<UploadSheet> Sheets { get; set; } = [];

    /// <summary>Años detectados en los estados financieros (columna JSON).</summary>
    public IList<int> Years { get; set; } = [];

    /// <summary>Resumen agregado: empleados, serie mensual y balance de comprobación (columna JSON).</summary>
    public UploadSummary Summary { get; set; } = new();

    /// <summary>Valores en bruto guardados (filas × años).</summary>
    public int RowsCount { get; set; }

    /// <summary>Resultado del análisis automático (homologación y consolidación).</summary>
    public AnalysisStatus AnalysisStatus { get; set; } = AnalysisStatus.NotStarted;

    /// <summary>"Revisión recomendada": hay cuentas de los estados con confianza menor a 0,8 o sin categoría.</summary>
    public bool NeedsReview { get; set; }

    /// <summary>Cantidad de cuentas dudosas.</summary>
    public int DoubtfulAccounts { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public ICollection<RawAccountValue> RawValues { get; } = [];
}

/// <summary>Error o advertencia de una carga, con su ubicación (hoja, fila y columna de Excel).</summary>
public sealed record UploadIssue(
    string Code,
    string Message,
    string? Sheet = null,
    int? Row = null,
    string? Column = null);

/// <summary>Resultado del análisis de una hoja.</summary>
public sealed record UploadSheet(
    string Name,
    SheetType DetectedType,
    int RowsExtracted,
    bool Ignored,
    string? Reason = null);

/// <summary>Resumen agregado de una carga. Nunca contiene datos personales.</summary>
public sealed record UploadSummary
{
    /// <summary>Empleados de la nómina (solo el conteo).</summary>
    public int? EmployeeCount { get; init; }

    /// <summary>Conteo y nómina anual por departamento (agregados).</summary>
    public IReadOnlyList<DepartmentSummary> EmployeesByDepartment { get; init; } = [];

    /// <summary>Suma de los sueldos del año (agregado de la nómina).</summary>
    public decimal? AnnualPayroll { get; init; }

    /// <summary>Meses con ventas en la serie mensual.</summary>
    public int MonthlyRecords { get; init; }

    /// <summary><c>false</c> si el balance de comprobación no cuadra o tiene saldos anómalos.</summary>
    public bool? TrialBalanceReliable { get; init; }

    /// <summary>Diferencia de la "prueba cero" del balance de comprobación.</summary>
    public decimal? TrialBalanceDifference { get; init; }
}

public sealed record DepartmentSummary(string Department, int Employees, decimal Payroll);
