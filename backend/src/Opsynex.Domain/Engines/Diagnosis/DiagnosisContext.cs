using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Engines.Diagnosis;

/// <summary>Montos de un año que usan las reglas.</summary>
public sealed record FinancialSnapshot
{
    public decimal? Revenue { get; init; }

    public decimal? TotalCost { get; init; }

    public decimal? OverheadCost { get; init; }

    public decimal? LaborCost { get; init; }

    public decimal? Cogs { get; init; }

    public decimal? MaterialLoss { get; init; }

    public decimal? EnergyCost { get; init; }
}

/// <summary>Todo lo que una regla necesita para evaluar un año (con el año anterior, si existe).</summary>
public sealed record DiagnosisContext
{
    public required int Year { get; init; }

    public int? PreviousYear { get; init; }

    public required Industry Industry { get; init; }

    public required IReadOnlyDictionary<KpiCode, decimal?> Kpis { get; init; }

    public IReadOnlyDictionary<KpiCode, decimal?>? PreviousKpis { get; init; }

    public required FinancialSnapshot Financial { get; init; }

    public FinancialSnapshot? PreviousFinancial { get; init; }

    public required DiagnosisSettings Settings { get; init; }

    public bool HasPrevious => PreviousYear is not null && PreviousKpis is not null && PreviousFinancial is not null;

    public decimal? Kpi(KpiCode code) => Kpis.GetValueOrDefault(code);

    public decimal? PreviousKpi(KpiCode code) => PreviousKpis?.GetValueOrDefault(code);

    /// <summary>Crecimiento porcentual año a año de un monto, o <c>null</c>.</summary>
    public static decimal? Growth(decimal? previous, decimal? current) =>
        previous is { } p && current is { } c && p > 0 ? (c - p) / p * 100m : null;
}
