using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Engines.Diagnosis;
using Opsynex.Domain.Engines.Impact;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Engines.Scoring;
using Opsynex.Domain.Entities;

namespace Opsynex.Application.Analysis;

/// <summary>Resultado de ejecutar el análisis de una empresa.</summary>
/// <param name="CompanyId">Empresa.</param>
/// <param name="Years">Años analizados.</param>
/// <param name="CalculatedAt">Fecha del cálculo (UTC).</param>
/// <param name="Problems">Problemas detectados en todos los años.</param>
/// <param name="LatestYear">Último año analizado.</param>
/// <param name="LatestScore">Score del último año (si se pudo calcular).</param>
/// <param name="LatestEstimatedLoss">Pérdida estimada total del último año.</param>
public sealed record AnalysisRunResult(
    Guid CompanyId,
    IReadOnlyList<int> Years,
    DateTime CalculatedAt,
    int Problems,
    int? LatestYear = null,
    decimal? LatestScore = null,
    decimal? LatestEstimatedLoss = null);

/// <summary>
/// Análisis completo de una empresa, año por año: KPIs (con tendencias y normalización) → diagnóstico
/// (problemas y hallazgos positivos) → impacto económico de cada problema → score Opsynex. Todo se
/// confirma con un único <c>SaveChanges</c> (una transacción). Se ejecuta al consolidar una carga y bajo
/// demanda (<c>POST /api/v1/analysis/run</c>).
/// </summary>
public sealed class AnalysisPipeline(
    ICompanyRepository companies,
    ICompanyYearRepository<FinancialData> financialData,
    ICompanyYearRepository<OperationalData> operationalData,
    ICompanyYearRepository<KpiResult> kpiResults,
    ICompanyYearRepository<Score> scores,
    IRepository<Problem> problems,
    DiagnosisEngine diagnosis,
    IUnitOfWork unitOfWork,
    IOptions<BenchmarkOptions> benchmarks,
    IOptions<DiagnosisSettings> diagnosisSettings,
    IOptions<ScoreSettings> scoreSettings,
    TimeProvider timeProvider)
{
    private sealed record YearData(
        IReadOnlyDictionary<KpiCode, decimal?> Values,
        IReadOnlyDictionary<KpiCode, decimal?> Normalized,
        GrowthInput Growth,
        FinancialSnapshot Snapshot);

    public async Task<AnalysisRunResult> RunAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await companies.GetByIdAsync(companyId, cancellationToken) ?? throw new NotFoundException("la empresa", companyId);
        var financial = await financialData.ListByCompanyAsync(companyId, cancellationToken);
        var operational = (await operationalData.ListByCompanyAsync(companyId, cancellationToken)).ToDictionary(o => o.Year);
        var existing = (await kpiResults.ListByCompanyAsync(companyId, cancellationToken)).ToDictionary(k => k.Year);
        var existingScores = (await scores.ListByCompanyAsync(companyId, cancellationToken)).ToDictionary(s => s.Year);
        var settings = benchmarks.Value;
        var ranges = settings.For(company.Industry, company.Currency);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var existingProblems = (await problems.ListAsync(p => p.CompanyId == companyId, cancellationToken)).ToList();
        var detectedCount = 0;
        var computed = new Dictionary<int, YearData>();
        var scored = new Dictionary<int, Score>();
        var losses = new Dictionary<int, decimal>();
        foreach (var data in financial.OrderBy(f => f.Year))
        {
            // 1. KPIs, tendencias y normalización.
            var ops = operational.GetValueOrDefault(data.Year);
            var values = KpiEngine.Calculate(ToInput(data, ops));
            var normalized = KpiNormalizer.Normalize(values, ranges);
            var growth = new GrowthInput(data.Revenue, data.TotalCost, data.OverheadCost);
            var snapshot = ToSnapshot(data, ops);
            var hasPrevious = computed.TryGetValue(data.Year - 1, out var prior);
            computed[data.Year] = new YearData(values, normalized, growth, snapshot);

            if (!existing.TryGetValue(data.Year, out var result))
            {
                result = new KpiResult { CompanyId = companyId, Year = data.Year };
                await kpiResults.AddAsync(result, cancellationToken);
            }

            result.SetValues(values);
            result.Trends = hasPrevious
                ? TrendAnalyzer.Analyze(data.Year, values, growth, data.Year - 1, prior!.Values, prior.Growth, settings.StableThresholdPercent)
                : new YearTrends { Year = data.Year };
            result.Normalized = normalized.ToDictionary(n => n.Key.ToString(), n => n.Value);
            result.CalculatedAt = now;

            // 2. Diagnóstico del año (con el año anterior, si existe).
            var context = new DiagnosisContext
            {
                Year = data.Year,
                PreviousYear = hasPrevious ? data.Year - 1 : null,
                Industry = company.Industry,
                Kpis = values,
                PreviousKpis = hasPrevious ? prior!.Values : null,
                Financial = snapshot,
                PreviousFinancial = hasPrevious ? prior!.Snapshot : null,
                Settings = diagnosisSettings.Value,
            };
            var diagnosed = diagnosis.Evaluate(context);
            result.Highlights = [.. diagnosed.Highlights];
            detectedCount += diagnosed.Problems.Count;

            // 3. Impacto económico de cada problema.
            var impacts = diagnosed.Problems.ToDictionary(p => p.Code, p => ImpactEngine.Estimate(p, context), StringComparer.Ordinal);
            losses[data.Year] = impacts.Values.Sum(i => i.EstimatedLoss ?? 0m);
            await ReplaceProblemsAsync(companyId, data.Year, diagnosed, impacts, company.Currency, existingProblems, cancellationToken);

            // 4. Score del año.
            var score = ScoreEngine.Calculate(ToScoreInput(data.Year, computed), scoreSettings.Value);
            existingScores.Remove(data.Year, out var entity);
            if (score.Total is null)
            {
                if (entity is not null)
                {
                    scores.Remove(entity);
                }

                continue;
            }

            if (entity is null)
            {
                entity = new Score { CompanyId = companyId, Year = data.Year };
                await scores.AddAsync(entity, cancellationToken);
            }

            Apply(entity, score);
            scored[data.Year] = entity;
        }

        // Resultados de años que ya no tienen datos financieros.
        problems.RemoveRange(existingProblems.Where(p => !computed.ContainsKey(p.Year)));
        kpiResults.RemoveRange(existing.Values.Where(k => !computed.ContainsKey(k.Year)));
        scores.RemoveRange(existingScores.Values);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        int? latest = computed.Count == 0 ? null : computed.Keys.Max();
        return new AnalysisRunResult(
            companyId,
            [.. computed.Keys.Order()],
            now,
            detectedCount,
            latest,
            latest is { } y && scored.TryGetValue(y, out var s) ? s.TotalScore : null,
            latest is { } ly ? losses[ly] : null);
    }

    /// <summary>Insumos del score de un año: KPIs normalizados, ingresos por empleado y serie de márgenes.</summary>
    private static ScoreInput ToScoreInput(int year, Dictionary<int, YearData> computed)
    {
        var current = computed[year];
        var previous = computed.GetValueOrDefault(year - 1);
        var margins = computed.Where(c => c.Key <= year)
            .OrderBy(c => c.Key)
            .Select(c => c.Value.Values.GetValueOrDefault(KpiCode.EbitdaMargin))
            .Where(m => m is not null)
            .Select(m => m!.Value)
            .ToList();
        return new ScoreInput
        {
            Normalized = current.Normalized,
            RevenuePerEmployee = current.Values.GetValueOrDefault(KpiCode.RevenuePerEmployee),
            PreviousRevenuePerEmployee = previous?.Values.GetValueOrDefault(KpiCode.RevenuePerEmployee),
            EbitdaMarginHistory = margins,
        };
    }

    private static void Apply(Score entity, ScoreResult score)
    {
        entity.TotalScore = score.Total!.Value;
        entity.Interpretation = score.Interpretation!.Value;
        entity.ProfitabilityScore = score.Component(KpiDimension.Profitability).Value;
        entity.CostEfficiencyScore = score.Component(KpiDimension.CostEfficiency).Value;
        entity.ProductivityScore = score.Component(KpiDimension.Productivity).Value;
        entity.FinancialStabilityScore = score.Component(KpiDimension.FinancialStability).Value;
        entity.WeightsUsed = score.Components.ToDictionary(c => c.Dimension.ToString(), c => c.Weight);
        entity.Components = [.. score.Components];
    }

    /// <summary>
    /// Reemplaza los problemas del año de forma idempotente: actualiza los que se vuelven a
    /// detectar (conservando el estado que puso el usuario), agrega los nuevos y elimina los que ya no aplican.
    /// </summary>
    private async Task ReplaceProblemsAsync(
        Guid companyId,
        int year,
        DiagnosisResult diagnosed,
        Dictionary<string, ImpactEstimate> impacts,
        string currency,
        List<Problem> existing,
        CancellationToken cancellationToken)
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
            var impact = impacts[detected.Code];
            problem.ImpactValue = impact.EstimatedLoss;
            problem.Recommendation = ImpactEngine.Sentence(impact, currency) is { } sentence
                ? $"{detected.Result.Recommendation} {sentence}"
                : detected.Result.Recommendation;
            var evidence = (JsonObject)detected.Result.Evidence.DeepClone();
            evidence["impact"] = impact.ToJson();
            problem.Evidence = evidence;
        }

        problems.RemoveRange(current.Values);
        existing.RemoveAll(p => p.Year == year);
    }

    public static FinancialSnapshot ToSnapshot(FinancialData data, OperationalData? operational = null)
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
            EnergyCost = data.EnergyCost ?? operational?.EnergyCost,
            EmployeeCount = operational?.EmployeeCount,
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
