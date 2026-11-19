using Microsoft.Extensions.Options;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Engines.Diagnosis;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Entities;

namespace Opsynex.Application.Analysis;

/// <summary>Resultado de ejecutar el análisis de una empresa.</summary>
public sealed record AnalysisRunResult(Guid CompanyId, IReadOnlyList<int> Years, DateTime CalculatedAt, int Problems);

/// <summary>
/// Análisis de una empresa: recalcula los KPIs de todos sus años (con tendencias y normalización)
/// y el diagnóstico (problemas y hallazgos positivos). En la etapa 07 agrega el impacto y el score. Se ejecuta al consolidar
/// una carga y bajo demanda (<c>POST /api/v1/analysis/run</c>).
/// </summary>
public sealed class AnalysisPipeline(
    ICompanyRepository companies,
    ICompanyYearRepository<FinancialData> financialData,
    ICompanyYearRepository<OperationalData> operationalData,
    ICompanyYearRepository<KpiResult> kpiResults,
    IRepository<Problem> problems,
    DiagnosisEngine diagnosis,
    IUnitOfWork unitOfWork,
    IOptions<BenchmarkOptions> benchmarks,
    IOptions<DiagnosisSettings> diagnosisSettings,
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

        var existingProblems = (await problems.ListAsync(p => p.CompanyId == companyId, cancellationToken)).ToList();
        var detectedCount = 0;
        var computed = new Dictionary<int, (IReadOnlyDictionary<KpiCode, decimal?> Values, GrowthInput Growth, FinancialSnapshot Snapshot)>();
        foreach (var data in financial.OrderBy(f => f.Year))
        {
            var ops = operational.GetValueOrDefault(data.Year);
            var values = KpiEngine.Calculate(ToInput(data, ops));
            var growth = new GrowthInput(data.Revenue, data.TotalCost, data.OverheadCost);
            var snapshot = ToSnapshot(data);
            computed[data.Year] = (values, growth, snapshot);

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

            // Diagnóstico del año (con el año anterior, si existe).
            var hasPrevious = computed.TryGetValue(data.Year - 1, out var prior);
            var diagnosed = diagnosis.Evaluate(new DiagnosisContext
            {
                Year = data.Year,
                PreviousYear = hasPrevious ? data.Year - 1 : null,
                Industry = company.Industry,
                Kpis = values,
                PreviousKpis = hasPrevious ? prior.Values : null,
                Financial = snapshot,
                PreviousFinancial = hasPrevious ? prior.Snapshot : null,
                Settings = diagnosisSettings.Value,
            });
            result.Highlights = [.. diagnosed.Highlights];
            detectedCount += diagnosed.Problems.Count;
            await ReplaceProblemsAsync(companyId, data.Year, diagnosed, existingProblems, cancellationToken);
        }

        // Problemas de años que ya no tienen datos financieros.
        problems.RemoveRange(existingProblems.Where(p => !computed.ContainsKey(p.Year)));

        // KPIs de años que ya no tienen datos financieros.
        kpiResults.RemoveRange(existing.Values.Where(k => !computed.ContainsKey(k.Year)));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AnalysisRunResult(companyId, [.. computed.Keys.Order()], now, detectedCount);
    }

    /// <summary>
    /// Reemplaza los problemas del año de forma idempotente: actualiza los que se vuelven a
    /// detectar (conservando el estado que puso el usuario), agrega los nuevos y elimina los que ya no aplican.
    /// </summary>
    private async Task ReplaceProblemsAsync(
        Guid companyId, int year, DiagnosisResult diagnosed, List<Problem> existing, CancellationToken cancellationToken)
    {
        var current = existing.Where(p => p.Year == year).ToDictionary(p => p.Code, StringComparer.Ordinal);
        foreach (var detected in diagnosed.Problems)
        {
            if (!current.Remove(detected.Code, out var problem))
            {
                problem = new Problem
                {
                    CompanyId = companyId,
                    Year = year,
                    Code = detected.Code,
                    Title = detected.Rule.Title,
                    Description = string.Empty,
                    Recommendation = string.Empty,
                };
                await problems.AddAsync(problem, cancellationToken);
            }

            problem.Title = detected.Rule.Title;
            problem.Category = detected.Rule.Category;
            problem.ImpactCategory = detected.Rule.ImpactCategory;
            problem.Severity = detected.Result.Severity;
            problem.Description = detected.Result.Description;
            problem.Recommendation = detected.Result.Recommendation;
            problem.Evidence = detected.Result.Evidence;
        }

        problems.RemoveRange(current.Values);
        existing.RemoveAll(p => p.Year == year);
    }

    public static FinancialSnapshot ToSnapshot(FinancialData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new FinancialSnapshot
        {
            Revenue = data.Revenue,
            TotalCost = data.TotalCost,
            OverheadCost = data.OverheadCost,
            LaborCost = data.LaborCost,
            Cogs = data.Cogs,
            MaterialLoss = data.MaterialLoss,
            EnergyCost = data.EnergyCost,
        };
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
