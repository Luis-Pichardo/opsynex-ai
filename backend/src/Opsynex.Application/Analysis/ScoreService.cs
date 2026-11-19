using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Engines.Scoring;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Analysis;

/// <summary>Componente del score en la respuesta.</summary>
public sealed record ScoreComponentDto(
    KpiDimension Dimension,
    string Name,
    decimal? Score,
    decimal BaseWeight,
    decimal Weight,
    IReadOnlyDictionary<string, decimal?> Inputs,
    string? MissingReason);

/// <summary>Score de un año con sus componentes, pesos usados e interpretación.</summary>
public sealed record ScoreYearDto(
    int Year,
    decimal TotalScore,
    ScoreInterpretation Interpretation,
    string InterpretationLabel,
    decimal? ChangeFromPrevious,
    bool WeightsRedistributed,
    IReadOnlyDictionary<string, decimal> WeightsUsed,
    IReadOnlyList<ScoreComponentDto> Components,
    DateTime CalculatedAt);

public sealed record ScoreResponse(Guid CompanyId, IReadOnlyList<ScoreYearDto> Years);

/// <summary>Consulta del score Opsynex (un año o la serie histórica).</summary>
public sealed class ScoreService(CompanyScope scope, ICompanyYearRepository<Score> scores)
{
    private static readonly Dictionary<KpiDimension, string> Names = new()
    {
        [KpiDimension.Profitability] = "Rentabilidad",
        [KpiDimension.CostEfficiency] = "Eficiencia de costos",
        [KpiDimension.Productivity] = "Productividad",
        [KpiDimension.FinancialStability] = "Estabilidad financiera",
    };

    public async Task<ScoreResponse> GetAsync(int? year, CancellationToken cancellationToken)
    {
        var companyId = scope.Require("score.company_required");
        if (year is < 1900 or > 2200)
        {
            throw new BusinessValidationException("score.invalid_year", "year", "El año debe estar entre 1900 y 2200.");
        }

        var all = await scores.ListByCompanyAsync(companyId, cancellationToken);
        var byYear = all.ToDictionary(s => s.Year);
        var items = all
            .Where(s => year is null || s.Year == year)
            .Select(s => ToDto(s, byYear.GetValueOrDefault(s.Year - 1)))
            .ToList();
        return new ScoreResponse(companyId, items);
    }

    public static string NameOf(KpiDimension dimension) => Names[dimension];

    private static ScoreYearDto ToDto(Score score, Score? previous) => new(
        score.Year,
        score.TotalScore,
        score.Interpretation,
        ScoreEngine.Label(score.Interpretation),
        previous is null ? null : score.TotalScore - previous.TotalScore,
        score.Components.Any(c => c.Value is null),
        new Dictionary<string, decimal>(score.WeightsUsed),
        [.. score.Components.Select(c => new ScoreComponentDto(
            c.Dimension, Names[c.Dimension], c.Value, c.BaseWeight, c.Weight, c.Inputs, c.MissingReason))],
        score.UpdatedAt);
}
