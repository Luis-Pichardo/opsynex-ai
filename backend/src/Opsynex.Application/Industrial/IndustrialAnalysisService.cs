using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Application.Insights;
using Opsynex.Domain.Engines.Industrial;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Industrial;

/// <summary>Resultado del análisis industrial de una empresa.</summary>
/// <param name="CompanyId">Empresa.</param>
/// <param name="HasData">Si la empresa tiene datos de producción.</param>
/// <param name="Years">Años con producción.</param>
/// <param name="Problems">Problemas industriales detectados (todos los años).</param>
/// <param name="Warnings">Avisos para el usuario (p. ej. máquinas sin estándar).</param>
public sealed record IndustrialRunResult(Guid CompanyId, bool HasData, IReadOnlyList<int> Years, int Problems, IReadOnlyList<string> Warnings);

/// <summary>
/// Análisis industrial: KPIs agregados por mes y año (planta, línea y máquina) → reglas
/// industriales con su impacto → problemas (en la misma tabla que los financieros, con sus códigos).
/// Todo se confirma con un único <c>SaveChanges</c>. El score industrial se calcula al consultarlo,
/// porque combina estos KPIs con el Score Opsynex del año.
/// </summary>
public sealed class IndustrialAnalysisService(
    ICompanyRepository companies,
    IRepository<ProductionData> production,
    IRepository<CostData> costs,
    IRepository<StandardData> standards,
    IRepository<IndustrialKpiResult> results,
    IRepository<Problem> problems,
    IUnitOfWork unitOfWork,
    IOptions<IndustrialSettings> settings,
    IOptions<AiOptions> aiOptions,
    IAiEnrichmentScheduler aiScheduler,
    TimeProvider timeProvider)
{
    public async Task<IndustrialRunResult> RunAsync(Guid companyId, bool scheduleAi, CancellationToken cancellationToken)
    {
        var company = await companies.GetByIdAsync(companyId, cancellationToken) ?? throw new NotFoundException("la empresa", companyId);
        var data = await LoadAsync(companyId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // 1. KPIs (se reemplazan todos: son pocos registros y siempre quedan coherentes con los datos).
        var aggregates = IndustrialKpiEngine.Aggregate(data.Production, data.Costs, data.Standards);
        results.RemoveRange(await results.ListAsync(r => r.CompanyId == companyId, cancellationToken));
        await results.AddRangeAsync(aggregates.Select(a => ToEntity(companyId, a, now)), cancellationToken);

        // 2. Diagnóstico e impacto por año.
        var existing = (await problems.ListAsync(p => p.CompanyId == companyId, cancellationToken))
            .Where(p => IndustrialDiagnosisEngine.Codes.Contains(p.Code))
            .ToDictionary(p => (p.Year, p.Code));
        var detected = 0;
        var years = data.Production.Select(p => p.Date.Year).Distinct().Order().ToList();
        foreach (var year in years)
        {
            var machines = aggregates.Where(a => a.Year == year && a.Month is null && a.Level == IndustrialLevel.Machine).ToList();
            var total = aggregates.FirstOrDefault(a => a.Year == year && a.Month is null && a.Level == IndustrialLevel.Total);
            var dates = data.Production.Where(p => p.Date.Year == year).Select(p => p.Date).ToList();
            var (from, to) = (dates.Min(), dates.Max());
            var found = IndustrialDiagnosisEngine.Evaluate(machines, total, from, to, settings.Value, company.Currency);
            detected += found.Count;
            foreach (var item in found)
            {
                if (!existing.Remove((year, item.Code), out var problem))
                {
                    problem = new Problem
                    {
                        CompanyId = companyId,
                        Year = year,
                        Code = item.Code,
                        Title = item.Title,
                        Description = item.Description,
                        Recommendation = item.Recommendation,
                    };
                    await problems.AddAsync(problem, cancellationToken);
                }

                problem.Title = item.Title;
                problem.Description = item.Description;
                problem.Category = item.Category;
                problem.ImpactCategory = item.ImpactCategory;
                problem.Severity = item.Severity;
                problem.ImpactValue = item.Impact.EstimatedLoss;
                var sentence = IndustrialDiagnosisEngine.Sentence(item.Impact, company.Currency, IndustrialDiagnosisEngine.PeriodLabel(from, to));
                problem.Recommendation = sentence is null ? item.Recommendation : $"{item.Recommendation} {sentence}";
                var evidence = (JsonObject)item.Evidence.DeepClone();
                evidence["impact"] = item.Impact.ToJson();
                problem.Evidence = evidence;
            }
        }

        // Los que ya no se detectan (o cuyo año ya no tiene datos) se eliminan.
        problems.RemoveRange(existing.Values);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (scheduleAi && years.Count > 0)
        {
            await ScheduleAiAsync(companyId, cancellationToken);
        }

        return new IndustrialRunResult(companyId, data.Production.Count > 0, years, detected, Warnings(data));
    }

    /// <summary>Programa la IA (anomalías de las series diarias de OEE y paradas, resumen y recomendaciones).</summary>
    public async Task ScheduleAiAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (aiOptions.Value.Enabled)
        {
            await aiScheduler.ScheduleAsync(companyId, null, force: false, cancellationToken);
        }
    }

    /// <summary>Registros industriales de la empresa como entradas de los motores.</summary>
    public async Task<IndustrialDataSet> LoadAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var productionRows = await production.ListAsync(p => p.CompanyId == companyId, cancellationToken);
        var costRows = await costs.ListAsync(c => c.CompanyId == companyId, cancellationToken);
        var standardRows = await standards.ListAsync(s => s.CompanyId == companyId, cancellationToken);
        return new IndustrialDataSet(
            [.. productionRows.Select(p => new ProductionRecord(p.Date, p.MachineId, p.LineId, p.UnitsProduced, p.DowntimeMinutes, p.PlannedMinutes, p.CycleTime, p.GoodUnits, p.TotalUnits))],
            [.. costRows.Select(c => new CostRecord(c.Date, c.MachineId, c.LaborCost, c.MaterialCost, c.EnergyCost, c.OverheadCost))],
            [.. standardRows.Select(s => new StandardRecord(s.MachineId, s.StandardCycleTime, s.StandardCost, s.CostPerMinute, s.ProcessName))]);
    }

    private static List<string> Warnings(IndustrialDataSet data)
    {
        var warnings = new List<string>();
        var machines = data.Production.Select(p => p.MachineId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var withoutStandard = machines.Where(m => !data.Standards.Any(s => string.Equals(s.MachineId, m, StringComparison.OrdinalIgnoreCase))).ToList();
        if (machines.Count > 0 && withoutStandard.Count > 0)
        {
            warnings.Add($"Sin estándar para {string.Join(", ", withoutStandard)}: no se calculan su rendimiento ni su OEE. Sube la plantilla de estándares.");
        }

        var withoutCost = machines.Where(m => !data.Costs.Any(c => string.Equals(c.MachineId, m, StringComparison.OrdinalIgnoreCase))).ToList();
        if (machines.Count > 0 && withoutCost.Count > 0)
        {
            warnings.Add($"Sin costos para {string.Join(", ", withoutCost)}: no se calcula su costo por unidad. Sube la plantilla de costos.");
        }

        return warnings;
    }

    private static IndustrialKpiResult ToEntity(Guid companyId, IndustrialAggregate a, DateTime now) => new()
    {
        CompanyId = companyId,
        Year = a.Year,
        Month = a.Month,
        Level = a.Level,
        MachineId = a.MachineId,
        LineId = a.LineId,
        PlannedMinutes = a.Sums.PlannedMinutes,
        DowntimeMinutes = a.Sums.DowntimeMinutes,
        TotalUnits = a.Sums.TotalUnits,
        GoodUnits = a.Sums.GoodUnits,
        UnitsProduced = a.Sums.UnitsProduced,
        Days = a.Sums.Days,
        LaborCost = a.Sums.Costs?.Labor,
        MaterialCost = a.Sums.Costs?.Material,
        EnergyCost = a.Sums.Costs?.Energy,
        OverheadCost = a.Sums.Costs?.Overhead,
        TotalCost = a.Sums.Costs?.Total,
        Oee = a.Kpis.Oee,
        Availability = a.Kpis.Availability,
        Performance = a.Kpis.Performance,
        Quality = a.Kpis.Quality,
        ScrapRate = a.Kpis.ScrapRate,
        DowntimeRatio = a.Kpis.DowntimeRatio,
        CapacityUtilization = a.Kpis.CapacityUtilization,
        CostPerUnit = a.Kpis.CostPerUnit,
        StandardCostPerUnit = a.Kpis.StandardCostPerUnit,
        DowntimeCost = a.Losses.DowntimeCost,
        ScrapCost = a.Losses.ScrapCost,
        SpeedLossCost = a.Losses.SpeedLossCost,
        CalculatedAt = now,
    };
}

/// <summary>Registros industriales de una empresa.</summary>
public sealed record IndustrialDataSet(
    IReadOnlyList<ProductionRecord> Production,
    IReadOnlyList<CostRecord> Costs,
    IReadOnlyList<StandardRecord> Standards);
