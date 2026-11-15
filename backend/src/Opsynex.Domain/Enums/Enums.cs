namespace Opsynex.Domain.Enums;

/// <summary>Tipo de industria de la empresa.</summary>
public enum Industry
{
    Manufacturing,
    Services,
}

/// <summary>Rol del usuario.</summary>
public enum UserRole
{
    /// <summary>Administra toda la plataforma (puede no pertenecer a una empresa).</summary>
    Admin,

    /// <summary>Carga datos, homologa y ejecuta análisis de su empresa.</summary>
    Analyst,

    /// <summary>Consulta los resultados de su empresa.</summary>
    Client,
}

/// <summary>Tipo de archivo cargado.</summary>
public enum UploadFileType
{
    IncomeStatement,
    BalanceSheet,
    CashFlow,
    CostStructure,
    Operational,
}

/// <summary>Estado de procesamiento de una carga.</summary>
public enum UploadStatus
{
    Pending,
    Validated,
    Failed,
    Processed,
}

/// <summary>Cómo se obtuvo un mapeo de cuenta.</summary>
public enum MappingMethod
{
    /// <summary>El usuario eligió la categoría.</summary>
    Manual,

    /// <summary>Coincidencia exacta con un sinónimo del catálogo.</summary>
    Exact,

    /// <summary>Coincidencia aproximada de texto (rapidfuzz).</summary>
    Fuzzy,

    /// <summary>Sugerencia del servicio de IA.</summary>
    Ai,
}

/// <summary>Severidad de un problema (sección 11 de las especificaciones).</summary>
public enum Severity
{
    /// <summary>Recomendación de monitoreo.</summary>
    Low,

    /// <summary>Ineficiencia moderada.</summary>
    Medium,

    /// <summary>Impacto financiero crítico.</summary>
    High,
}

/// <summary>Categoría de impacto financiero (sección 12 de las especificaciones).</summary>
public enum ImpactCategory
{
    MarginLoss,
    ExcessCost,
    ProductivityLoss,
    OperationalWaste,
}

/// <summary>Área del negocio afectada por un problema (alineada con las dimensiones del score).</summary>
public enum ProblemCategory
{
    Profitability,
    CostEfficiency,
    Productivity,
    FinancialStability,
    Operations,
}

/// <summary>Estado de seguimiento de un problema.</summary>
public enum ProblemStatus
{
    Open,
    Acknowledged,
    Resolved,
}

/// <summary>Interpretación del score Opsynex (GUIA_DE_DISENO.md §2.3).</summary>
public enum ScoreInterpretation
{
    /// <summary>0–59: deterioro operacional significativo.</summary>
    Deteriorated,

    /// <summary>60–79: oportunidades de mejora.</summary>
    Improvable,

    /// <summary>80–100: operación saludable.</summary>
    Healthy,
}
