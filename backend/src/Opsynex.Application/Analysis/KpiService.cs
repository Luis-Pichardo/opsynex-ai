using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Analysis;

/// <summary>KPIs de un año: valores, normalizados (0–100) y tendencias frente al año anterior.</summary>
public sealed record KpiYearDto(
    int Year,
    IReadOnlyDictionary<KpiCode, decimal?> Values,
    IReadOnlyDictionary<string, decimal?> Normalized,
    YearTrends? Trends,
    DateTime CalculatedAt);

public sealed record KpisResponse(
    Guid CompanyId,
    Industry Industry,
    string Currency,
    IReadOnlyList<KpiDefinition> Definitions,
    IReadOnlyList<KpiYearDto> Years,
    IReadOnlyList<MonthlyPoint>? Monthly);

/// <summary>Consulta de KPIs (serie histórica o un año) y de la serie mensual.</summary>
public sealed class KpiService(
    CompanyScope scope,
    ICompanyRepository companies,
    ICompanyYearRepository<KpiResult> kpiResults,
    IRepository<MonthlyFinancialData> monthly)
{
    public async Task<KpisResponse> GetAsync(int? year, CancellationToken cancellationToken)
    {
        var companyId = scope.Require("kpis.company_required");
        var company = await companies.GetByIdAsync(companyId, cancellationToken) ?? throw new NotFoundException("la empresa", companyId);

        var results = (await kpiResults.ListByCompanyAsync(companyId, cancellationToken))
            .Where(k => year is null || k.Year == year)
            .Select(k => new KpiYearDto(k.Year, k.Values(), new Dictionary<string, decimal?>(k.Normalized),
                k.Trends.PreviousYear == 0 ? null : k.Trends, k.CalculatedAt))
            .ToList();

        var months = await monthly.ListAsync(m => m.CompanyId == companyId && (year == null || m.Year == year), cancellationToken);
        var series = months.Count == 0
            ? null
            : MonthlyAnalyzer.Analyze(months.Select(m => new MonthlyRevenue(m.Year, m.Month, m.Revenue)));

        return new KpisResponse(companyId, company.Industry, company.Currency, KpiCatalog.All, results, series);
    }
}
