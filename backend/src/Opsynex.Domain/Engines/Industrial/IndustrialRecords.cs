using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Engines.Industrial;

/// <summary>Producción de una máquina en un día (tiempos en minutos, ciclo en segundos/unidad).</summary>
public sealed record ProductionRecord(
    DateOnly Date,
    string MachineId,
    string? LineId,
    decimal UnitsProduced,
    decimal DowntimeMinutes,
    decimal PlannedMinutes,
    decimal? CycleTime,
    decimal GoodUnits,
    decimal TotalUnits);

/// <summary>Costos de una máquina en una fecha.</summary>
public sealed record CostRecord(
    DateOnly Date,
    string MachineId,
    decimal LaborCost,
    decimal MaterialCost,
    decimal EnergyCost,
    decimal OverheadCost)
{
    public decimal Total => LaborCost + MaterialCost + EnergyCost + OverheadCost;
}

/// <summary>Estándar de una máquina: ciclo (segundos/unidad), costo por unidad y costo por minuto.</summary>
public sealed record StandardRecord(
    string MachineId,
    decimal StandardCycleTime,
    decimal? StandardCost,
    decimal? CostPerMinute,
    string? ProcessName = null);

/// <summary>Sumas de un agregado industrial.</summary>
/// <param name="PlannedMinutes">Minutos planificados.</param>
/// <param name="DowntimeMinutes">Minutos de parada.</param>
/// <param name="TotalUnits">Unidades procesadas.</param>
/// <param name="GoodUnits">Unidades buenas.</param>
/// <param name="UnitsProduced">Unidades producidas (entregadas).</param>
/// <param name="Days">Días con producción.</param>
/// <param name="IdealMinutes">Σ ciclo estándar × unidades procesadas / 60 de las máquinas con estándar.</param>
/// <param name="RunMinutesWithStandard">Tiempo en marcha de las máquinas con estándar.</param>
/// <param name="PlannedMinutesWithStandard">Tiempo planificado de las máquinas con estándar.</param>
/// <param name="Costs">Costos por tipo (null si no hay costos del período).</param>
/// <param name="UnitsWithCost">Unidades producidas de las máquinas con costos.</param>
/// <param name="StandardCostAmount">Σ costo estándar × unidades producidas.</param>
/// <param name="UnitsWithStandardCost">Unidades producidas de las máquinas con costo estándar.</param>
public sealed record IndustrialSums(
    decimal PlannedMinutes,
    decimal DowntimeMinutes,
    decimal TotalUnits,
    decimal GoodUnits,
    decimal UnitsProduced,
    int Days,
    decimal? IdealMinutes,
    decimal RunMinutesWithStandard,
    decimal PlannedMinutesWithStandard,
    CostBreakdown? Costs,
    decimal UnitsWithCost,
    decimal? StandardCostAmount,
    decimal UnitsWithStandardCost)
{
    public decimal RunMinutes => Math.Max(0m, PlannedMinutes - DowntimeMinutes);

    public decimal ScrapUnits => Math.Max(0m, TotalUnits - GoodUnits);
}

/// <summary>Costos por tipo.</summary>
public sealed record CostBreakdown(decimal Labor, decimal Material, decimal Energy, decimal Overhead)
{
    public static readonly CostBreakdown Zero = new(0m, 0m, 0m, 0m);

    public decimal Total => Labor + Material + Energy + Overhead;

    public CostBreakdown Add(CostBreakdown other) =>
        new(Labor + other.Labor, Material + other.Material, Energy + other.Energy, Overhead + other.Overhead);
}

/// <summary>KPIs industriales (ratios de 0 a 1 salvo los costos, que son montos).</summary>
public sealed record IndustrialKpis(
    decimal? Availability,
    decimal? Performance,
    decimal? Quality,
    decimal? Oee,
    decimal? ScrapRate,
    decimal? DowntimeRatio,
    decimal? CapacityUtilization,
    decimal? CostPerUnit,
    decimal? StandardCostPerUnit);

/// <summary>Pérdidas valoradas. <see cref="Total"/> = paradas + scrap (fórmula de la especificación).</summary>
public sealed record IndustrialLosses(decimal? DowntimeCost, decimal? ScrapCost, decimal? SpeedLossCost)
{
    public static readonly IndustrialLosses None = new(null, null, null);

    public decimal? Total => DowntimeCost is null && ScrapCost is null ? null : (DowntimeCost ?? 0m) + (ScrapCost ?? 0m);

    public IndustrialLosses Add(IndustrialLosses other) =>
        new(Sum(DowntimeCost, other.DowntimeCost), Sum(ScrapCost, other.ScrapCost), Sum(SpeedLossCost, other.SpeedLossCost));

    private static decimal? Sum(decimal? a, decimal? b) => a is null && b is null ? null : (a ?? 0m) + (b ?? 0m);
}

/// <summary>Agregado de un período (año o mes) y un alcance (planta, línea o máquina).</summary>
public sealed record IndustrialAggregate(
    int Year,
    int? Month,
    IndustrialLevel Level,
    string? MachineId,
    string? LineId,
    IndustrialSums Sums,
    IndustrialKpis Kpis,
    IndustrialLosses Losses,
    decimal? CostPerMinute = null,
    decimal? UnitCost = null)
{
    public string Period => Month is { } m ? $"{Year:0000}-{m:00}" : $"{Year:0000}";
}

/// <summary>Punto diario de la planta (o del filtro aplicado) para las series y la detección de anomalías.</summary>
public sealed record IndustrialDailyPoint(DateOnly Date, decimal? Oee, decimal DowntimeMinutes, decimal? Availability, decimal? Performance, decimal? Quality);
