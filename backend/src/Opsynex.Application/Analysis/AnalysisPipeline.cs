using Microsoft.Extensions.Options;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Entities;

namespace Opsynex.Application.Analysis;

/// <summary>Resultado de ejecutar el análisis de una empresa.</summary>
public sealed record AnalysisRunResult(Guid CompanyId, IReadOnlyList<int> Years, DateTime CalculatedAt);

/// <summary>
/// Análisis de una empresa: recalcula los KPIs de todos sus años (con tendencias y normalización).
/// En las etapas siguientes agrega el diagnóstico, el impacto y el score. Se ejecuta al consolidar
/// una carga y bajo demanda (<c>POST /api/v1/analysis/run</c>).
/// </summary>
public sealed class AnalysisPipeline(
    ICompanyRepository companies,
    ICompanyYearRepository<FinancialData> financialData,
    ICompanyYearRepository<OperationalData> operationalData,
    ICompanyYearRepository<KpiResult> kpiResults,
    IUnitOfWork unitOfWork,
    IOptions<BenchmarkOptions> benchmarks,
    TimeProvider timeProvider)
{
    public async Task<AnalysisRunResult> RunAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await companies.GetByIdAsync(companyId, cancellationToken) ?? throw new NotFoundException("la empresa", companyId);
        var financial = await financialData.ListByCompanyAsync(companyId, cancellationToken);
        var operational = (await operationalData.ListByCompanyAsync(companyId, cancellationToken)).ToDictionary(o => o.Year);
        var existing = (await kpiResults.ListByCompanyAsync(companyId, cancellationToken)).ToDictionary(k => k.Year);
        var settings = benchmarks.Value;
        var ranges = settings.For(company.Industry, company.Currency);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var computed = new Dictionary<int, (IReadOnlyDictionary<KpiCode, decimal?> Values, GrowthInput Growth)>();
        foreach (var data in financial.OrderBy(f => f.Year))
        {
            var ops = operational.GetValueOrDefault(data.Year);
            var values = KpiEngine.Calculate(ToInput(data, ops));
            var growth = new GrowthInput(data.Revenue, data.TotalCost, data.OverheadCost);
            computed[data.Year] = (values, growth);

            if (!existing.TryGetValue(data.Year, out var result))
            {
                result = new KpiResult { CompanyId = companyId, Year = data.Year };
                await kpiResults.AddAsync(result, cancellationToken);
            }

            result.SetValues(values);
            result.Trends = computed.TryGetValue(data.Year - 1, out var previous)
                ? TrendAnalyzer.Analyze(data.Year, values, growth, data.Year - 1, previous.Values, previous.Growth, settings.StableThresholdPercent)
                : new YearTrends { Year = data.Year };
            result.Normalized = KpiNormalizer.Normalize(values, ranges).ToDictionary(n => n.Key.ToString(), n => n.Value);
            result.CalculatedAt = now;
        }

        // KPIs de años que ya no tienen datos financieros.
        kpiResults.RemoveRange(existing.Values.Where(k => !computed.ContainsKey(k.Year)));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AnalysisRunResult(companyId, [.. computed.Keys.Order()], now);
    }

    public static KpiInput ToInput(FinancialData data, OperationalData? operational)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new KpiInput
        {
            Revenue = data.Revenue,
            Cogs = data.Cogs,
            TotalCost = data.TotalCost,
            OperatingExpenses = data.OperatingExpenses,
            Ebitda = data.Ebitda,
            OverheadCost = data.OverheadCost,
            EnergyCost = data.EnergyCost ?? operational?.EnergyCost,
            NetIncome = data.NetIncome,
            LaborCost = data.LaborCost,
            CurrentAssets = data.CurrentAssets,
            CurrentLiabilities = data.CurrentLiabilities,
            TotalLiabilities = data.TotalLiabilities,
            Equity = data.Equity,
            AccountsReceivable = data.AccountsReceivable,
            Inventory = data.Inventory,
            EmployeeCount = operational?.EmployeeCount,
            UnitsProduced = operational?.UnitsProduced,
        };
    }
}
