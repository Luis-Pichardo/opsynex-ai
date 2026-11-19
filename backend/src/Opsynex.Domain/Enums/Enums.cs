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
    /// <summary>Libro contable completo de varias hojas (balance, resultados, notas, nómina…).</summary>
    AccountingWorkbook,

    IncomeStatement,
    BalanceSheet,
    CashFlow,
    CostStructure,
    Operational,
}

/// <summary>Tipo de hoja detectado por el clasificador (por su contenido, no por su nombre).</summary>
public enum SheetType
{
    Unknown,
    BalanceSheet,
    IncomeStatement,
    EquityStatement,
    Notes,
    EmployeeRoster,
    MonthlySales,
    TrialBalance,
    CashFlow,
    CostStructure,
    Operational,
}

/// <summary>Clase de fila de un estado financiero.</summary>
public enum RowKind
{
    /// <summary>Cuenta con valores (se homologa).</summary>
    Detail,

    /// <summary>Subtotal ("Total corriente", líneas sin etiqueta con valores): solo para validar.</summary>
    Subtotal,

    /// <summary>Gran total ("Total Activos", "Total Pasivos &amp; Capital"): solo para validar.</summary>
    Total,
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

    /// <summary>Palabra clave del diccionario (servicio de IA).</summary>
    Keyword,

    /// <summary>Regla de contexto (p. ej. "Total corriente" según la sección).</summary>
    Rule,

    /// <summary>Mapeo ya confirmado de la empresa, reutilizado.</summary>
    Known,

    /// <summary>Diccionario mínimo local del API (el servicio de IA no respondió).</summary>
    Fallback,

    /// <summary>Sin coincidencia: quedó en <c>Other</c> y se recomienda revisarla.</summary>
    Unmatched,
}

/// <summary>Alcance de un mapeo: líneas de los estados financieros o detalles de las notas.</summary>
public enum MappingScope
{
    Statement,
    Notes,
}

/// <summary>
/// Rol del mapeo en la consolidación (evita el doble conteo): <c>Primary</c> alimenta los totales
/// (ingresos, costos, activos…); <c>Breakdown</c> solo las categorías analíticas (energía,
/// mantenimiento, personal, materias primas…).
/// </summary>
public enum MappingRole
{
    Primary,
    Breakdown,
}

/// <summary>Estado del análisis automático de una carga.</summary>
public enum AnalysisStatus
{
    NotStarted,
    Completed,
    Failed,
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

    /// <summary>Alerta financiera (liquidez, endeudamiento): no se cuantifica como pérdida.</summary>
    FinancialAlert,
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
