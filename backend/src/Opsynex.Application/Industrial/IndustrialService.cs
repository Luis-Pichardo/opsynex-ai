using Microsoft.Extensions.Options;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Analysis;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Engines.Industrial;
using Opsynex.Domain.Engines.Scoring;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Industrial;

/// <summary>Filtros de las consultas industriales.</summary>
/// <param name="Year">Año (el último con datos si no se indica).</param>
/// <param name="Month">Mes (1–12); sin mes, el año completo.</param>
/// <param name="MachineId">Máquina.</param>
/// <param name="LineId">Línea.</param>
public sealed record IndustrialFilter(int? Year = null, int? Month = null, string? MachineId = null, string? LineId = null);

public sealed record IndustrialKpiValuesDto(
    decimal? Oee,
    decimal? Availability,
    decimal? Performance,
    decimal? Quality,
    decimal? ScrapRate,
    decimal? DowntimeRatio,
    decimal? CapacityUtilization,
    decimal? CostPerUnit,
    decimal? StandardCostPerUnit);

public sealed record IndustrialCostsDto(decimal? Labor, decimal? Material, decimal? Energy, decimal? Overhead, decimal? Total);

/// <summary>Pérdidas valoradas; <c>Total</c> = paradas + scrap (fórmula de la especificación).</summary>
public sealed record IndustrialLossesDto(decimal? DowntimeCost, decimal? ScrapCost, decimal? SpeedLossCost, decimal? Total);

/// <summary>Fila de KPIs industriales de un período y un alcance.</summary>
public sealed record IndustrialRowDto(
    string Period,
    int Year,
    int? Month,
    IndustrialLevel Level,
    string? MachineId,
    string? LineId,
    decimal PlannedMinutes,
    decimal DowntimeMinutes,
    decimal TotalUnits,
    decimal GoodUnits,
    decimal UnitsProduced,
    int Days,
    IndustrialKpiValuesDto Kpis,
    IndustrialCostsDto? Costs,
    IndustrialLossesDto Losses);

public sealed record IndustrialDailyDto(string Date, decimal? Oee, decimal DowntimeMinutes, decimal? Availability, decimal? Performance, decimal? Quality);

public sealed record IndustrialMachineDto(string MachineId, string? LineId, string? ProcessName, bool HasStandard, bool HasCosts);

/// <summary>Umbrales de las reglas (para las referencias de los gráficos).</summary>
public sealed record IndustrialThresholdsDto(decimal LowOee, decimal HighScrap, decimal HighDowntime, decimal CostTolerance);

/// <summary>Respuesta de <c>GET /industrial/kpis</c>.</summary>
public sealed record IndustrialKpisDto(
    Guid CompanyId,
    bool HasData,
    string Currency,
    IReadOnlyList<int> Years,
    int? Year,
    int? Month,
    string? MachineId,
    string? LineId,
    IReadOnlyList<int> Months,
    IReadOnlyList<IndustrialMachineDto> Machines,
    IReadOnlyList<string> Lines,
    IndustrialRowDto? Summary,
    IReadOnlyList<IndustrialRowDto> ByMachine,
    IReadOnlyList<IndustrialRowDto> ByLine,
    IReadOnlyList<IndustrialRowDto> Monthly,
    IReadOnlyList<IndustrialDailyDto> Daily,
    IndustrialThresholdsDto Thresholds,
    IReadOnlyList<string> Warnings);

public sealed record IndustrialScoreComponentDto(
    IndustrialDimension Dimension,
    string Name,
    decimal? Score,
    decimal BaseWeight,
    decimal Weight,
    IReadOnlyDictionary<string, decimal?> Inputs,
    string? MissingReason);

/// <summary>Respuesta de <c>GET /industrial/score</c>.</summary>
public sealed record IndustrialScoreDto(
    Guid CompanyId,
    bool HasData,
    int? Year,
    int? Month,
    string? MachineId,
    string? LineId,
    decimal? TotalScore,
    ScoreInterpretation? Interpretation,
    string? InterpretationLabel,
    bool WeightsRedistributed,
    IReadOnlyList<IndustrialScoreComponentDto> Components,
    decimal? OpsynexScore,
    string? Message);

/// <summary>Consultas del módulo industrial (KPIs y score) con filtros por período, máquina y línea.</summary>
public sealed class IndustrialService(
    CompanyScope scope,
    ICompanyRepository companies,
    IRepository<IndustrialKpiResult> results,
    IRepository<StandardData> standards,
    IRepository<CostData> costs,
    ICompanyYearRepository<Score> scores,
    IndustrialAnalysisService analysis,
    IOptions<IndustrialSettings> settings)
{
    private static readonly Dictionary<IndustrialDimension, string> Names = new()
    {
        [IndustrialDimension.Production] = "Producción (OEE)",
        [IndustrialDimension.Cost] = "Costo frente al estándar",
        [IndustrialDimension.Financial] = "Score financiero",
        [IndustrialDimension.Standard] = "Cumplimiento de estándares",
    };

    public static string NameOf(IndustrialDimension dimension) => Names[dimension];

    public async Task<IndustrialKpisDto> GetKpisAsync(IndustrialFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var companyId = scope.Require("industrial.company_required");
        var company = await companies.GetByIdAsync(companyId, cancellationToken) ?? throw new NotFoundException("la empresa", companyId);
        var rows = await results.ListAsync(r => r.CompanyId == companyId, cancellationToken);
        var thresholds = Thresholds();
        if (rows.Count == 0)
        {
            Validate(filter);
            return new IndustrialKpisDto(companyId, false, company.Currency, [], null, null, null, null, [], [], [], null, [], [], [], [], thresholds, []);
        }

        var resolved = Resolve(filter, rows);
        var (year, month, machine, line) = (resolved.Year!.Value, resolved.Month, resolved.MachineId, resolved.LineId);
        var period = rows.Where(r => r.Year == year && r.Month == month).ToList();
        var level = machine is not null ? IndustrialLevel.Machine : line is not null ? IndustrialLevel.Line : IndustrialLevel.Total;
        bool InScope(IndustrialKpiResult r) => level switch
        {
            IndustrialLevel.Machine => r.Level == IndustrialLevel.Machine && Same(r.MachineId, machine),
            IndustrialLevel.Line => r.Level == IndustrialLevel.Line && Same(r.LineId, line),
            _ => r.Level == IndustrialLevel.Total,
        };

        var summary = period.FirstOrDefault(InScope);
        var byMachine = period
            .Where(r => r.Level == IndustrialLevel.Machine && (line is null || Same(r.LineId, line)) && (machine is null || Same(r.MachineId, machine)))
            .OrderBy(r => r.MachineId, StringComparer.OrdinalIgnoreCase)
            .Select(ToDto)
            .ToList();
        var byLine = period.Where(r => r.Level == IndustrialLevel.Line).OrderBy(r => r.LineId, StringComparer.OrdinalIgnoreCase).Select(ToDto).ToList();
        var monthly = rows.Where(r => r.Year == year && r.Month is not null && InScope(r)).OrderBy(r => r.Month).Select(ToDto).ToList();

        // Serie diaria del alcance (se calcula con los registros del período).
        var data = await analysis.LoadAsync(companyId, cancellationToken);
        var machineLines = MachineLines(rows);
        var daily = IndustrialKpiEngine.Daily(
                [.. data.Production.Where(p => p.Date.Year == year && (month is null || p.Date.Month == month)
                    && (machine is null || Same(p.MachineId, machine))
                    && (line is null || Same(machineLines.GetValueOrDefault(p.MachineId), line)))],
                data.Standards)
            .Select(d => new IndustrialDailyDto(d.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), d.Oee, d.DowntimeMinutes, d.Availability, d.Performance, d.Quality))
            .ToList();

        var standardRows = await standards.ListAsync(s => s.CompanyId == companyId, cancellationToken);
        var costMachines = (await costs.ListAsync(c => c.CompanyId == companyId, cancellationToken)).Select(c => c.MachineId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var machines = machineLines
            .OrderBy(m => m.Key, StringComparer.OrdinalIgnoreCase)
            .Select(m =>
            {
                var standard = standardRows.FirstOrDefault(s => Same(s.MachineId, m.Key));
                return new IndustrialMachineDto(m.Key, m.Value, standard?.ProcessName, standard is not null, costMachines.Contains(m.Key));
            })
            .ToList();
        var warnings = new List<string>();
        if (machines.Any(m => !m.HasStandard))
        {
            warnings.Add($"Sin estándar para {string.Join(", ", machines.Where(m => !m.HasStandard).Select(m => m.MachineId))}: no se calculan su rendimiento ni su OEE.");
        }

        if (machines.Any(m => !m.HasCosts))
        {
            warnings.Add($"Sin costos para {string.Join(", ", machines.Where(m => !m.HasCosts).Select(m => m.MachineId))}: no se calcula su costo por unidad.");
        }

        return new IndustrialKpisDto(
            companyId,
            true,
            company.Currency,
            [.. rows.Select(r => r.Year).Distinct().Order()],
            year,
            month,
            machine,
            line,
            [.. rows.Where(r => r.Year == year && r.Month is not null).Select(r => r.Month!.Value).Distinct().Order()],
            machines,
            [.. machineLines.Values.Where(l => l is not null).Select(l => l!).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)],
            summary is null ? null : ToDto(summary),
            byMachine,
            byLine,
            monthly,
            daily,
            thresholds,
            warnings);
    }

    public async Task<IndustrialScoreDto> GetScoreAsync(IndustrialFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var companyId = scope.Require("industrial.company_required");
        var rows = await results.ListAsync(r => r.CompanyId == companyId, cancellationToken);
        if (rows.Count == 0)
        {
            Validate(filter);
            return new IndustrialScoreDto(companyId, false, null, null, null, null, null, null, null, false, [], null,
                "La empresa no tiene datos industriales. Sube la producción, los costos y los estándares.");
        }

        var resolved = Resolve(filter, rows);
        var (year, month) = (resolved.Year!.Value, resolved.Month);
        var row = rows.FirstOrDefault(r => r.Year == year && r.Month == month && (
            resolved.MachineId is not null ? r.Level == IndustrialLevel.Machine && Same(r.MachineId, resolved.MachineId)
            : resolved.LineId is not null ? r.Level == IndustrialLevel.Line && Same(r.LineId, resolved.LineId)
            : r.Level == IndustrialLevel.Total));
        var opsynex = await scores.GetByCompanyAndYearAsync(companyId, year, cancellationToken);
        var result = IndustrialScoreEngine.Calculate(
            new IndustrialScoreInput(row?.Oee, row?.Performance, row?.ScrapRate, row?.CostPerUnit, row?.StandardCostPerUnit, opsynex?.TotalScore),
            settings.Value);

        return new IndustrialScoreDto(
            companyId,
            true,
            year,
            month,
            resolved.MachineId,
            resolved.LineId,
            result.Total,
            result.Interpretation,
            result.Interpretation is { } i ? ScoreEngine.Label(i) : null,
            result.WeightsRedistributed,
            [.. result.Components.Select(c => new IndustrialScoreComponentDto(c.Dimension, Names[c.Dimension], c.Value, c.BaseWeight, c.Weight, c.Inputs, c.MissingReason))],
            opsynex?.TotalScore,
            result.Total is null ? "No hay datos suficientes para el score industrial del período." : null);
    }

    private IndustrialThresholdsDto Thresholds()
    {
        var s = settings.Value;
        return new IndustrialThresholdsDto(s.LowOeeThreshold, s.HighScrapThreshold, s.HighDowntimeThreshold, s.CostAboveStandardTolerance);
    }

    /// <summary>Valida los filtros y completa el año (el último con datos).</summary>
    private static IndustrialFilter Resolve(IndustrialFilter filter, IReadOnlyList<IndustrialKpiResult> rows)
    {
        Validate(filter);
        var year = filter.Year ?? rows.Max(r => r.Year);
        if (!rows.Any(r => r.Year == year))
        {
            throw new BusinessValidationException("industrial.no_data_for_year", "year", $"No hay datos industriales de {year}.");
        }

        if (filter.Month is { } month && !rows.Any(r => r.Year == year && r.Month == month))
        {
            throw new BusinessValidationException("industrial.no_data_for_month", "month", $"No hay datos industriales de {month:00}/{year}.");
        }

        var machine = Clean(filter.MachineId);
        var line = Clean(filter.LineId);
        if (machine is not null && !rows.Any(r => r.Level == IndustrialLevel.Machine && Same(r.MachineId, machine)))
        {
            throw new BusinessValidationException("industrial.unknown_machine", "machineId", $"No hay datos de la máquina «{machine}».");
        }

        if (line is not null && !rows.Any(r => r.Level == IndustrialLevel.Line && Same(r.LineId, line)))
        {
            throw new BusinessValidationException("industrial.unknown_line", "lineId", $"No hay datos de la línea «{line}».");
        }

        return new IndustrialFilter(year, filter.Month, machine, line);
    }

    private static void Validate(IndustrialFilter filter)
    {
        if (filter.Year is < 1900 or > 2200)
        {
            throw new BusinessValidationException("industrial.invalid_year", "year", "El año debe estar entre 1900 y 2200.");
        }

        if (filter.Month is < 1 or > 12)
        {
            throw new BusinessValidationException("industrial.invalid_month", "month", "El mes debe estar entre 1 y 12.");
        }
    }

    private static Dictionary<string, string?> MachineLines(IEnumerable<IndustrialKpiResult> rows) =>
        rows.Where(r => r.Level == IndustrialLevel.Machine && r.MachineId is not null)
            .GroupBy(r => r.MachineId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Year).ThenBy(r => r.Month ?? 13).Last().LineId, StringComparer.OrdinalIgnoreCase);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static IndustrialRowDto ToDto(IndustrialKpiResult r) => new(
        r.Period,
        r.Year,
        r.Month,
        r.Level,
        r.MachineId,
        r.LineId,
        r.PlannedMinutes,
        r.DowntimeMinutes,
        r.TotalUnits,
        r.GoodUnits,
        r.UnitsProduced,
        r.Days,
        new IndustrialKpiValuesDto(r.Oee, r.Availability, r.Performance, r.Quality, r.ScrapRate, r.DowntimeRatio, r.CapacityUtilization, r.CostPerUnit, r.StandardCostPerUnit),
        r.TotalCost is null ? null : new IndustrialCostsDto(r.LaborCost, r.MaterialCost, r.EnergyCost, r.OverheadCost, r.TotalCost),
        new IndustrialLossesDto(r.DowntimeCost, r.ScrapCost, r.SpeedLossCost,
            r.DowntimeCost is null && r.ScrapCost is null ? null : (r.DowntimeCost ?? 0m) + (r.ScrapCost ?? 0m)));
}
