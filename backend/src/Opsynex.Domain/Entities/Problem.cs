using System.Text.Json.Nodes;
using Opsynex.Domain.Common;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Entities;

/// <summary>Ineficiencia detectada por el motor de diagnóstico.</summary>
public sealed class Problem : Entity, ICompanyYearScoped
{
    public Guid CompanyId { get; set; }

    public int Year { get; set; }

    /// <summary>Código de la regla que lo detectó (p. ej. <c>EBITDA_DECLINE</c>).</summary>
    public required string Code { get; set; }

    public required string Title { get; set; }

    public required string Description { get; set; }

    public ProblemCategory Category { get; set; }

    public Severity Severity { get; set; }

    public ImpactCategory ImpactCategory { get; set; }

    /// <summary>Pérdida económica estimada (etapa 07).</summary>
    public decimal? ImpactValue { get; set; }

    /// <summary>Recomendación base del motor de reglas.</summary>
    public required string Recommendation { get; set; }

    /// <summary>Recomendación enriquecida por IA (etapa 11).</summary>
    public string? AiRecommendation { get; set; }

    /// <summary>Evidencia del diagnóstico y del cálculo de impacto (columna JSON).</summary>
    public JsonObject Evidence { get; set; } = [];

    public ProblemStatus Status { get; set; } = ProblemStatus.Open;
}
