using Opsynex.Domain.Common;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Entities;

/// <summary>Score Opsynex (0–100) de una empresa para un año.</summary>
public sealed class Score : Entity, ICompanyYearScoped
{
    public Guid CompanyId { get; set; }

    public int Year { get; set; }

    public decimal TotalScore { get; set; }

    public decimal ProfitabilityScore { get; set; }

    public decimal CostEfficiencyScore { get; set; }

    public decimal ProductivityScore { get; set; }

    public decimal FinancialStabilityScore { get; set; }

    /// <summary>Pesos usados por dimensión (columna JSON), p. ej. { "Profitability": 0.35 }.</summary>
    public IDictionary<string, decimal> WeightsUsed { get; set; } = new Dictionary<string, decimal>();

    public ScoreInterpretation Interpretation { get; set; }
}
