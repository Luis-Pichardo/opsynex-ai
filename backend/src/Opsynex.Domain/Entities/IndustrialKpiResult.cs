using Opsynex.Domain.Common;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Entities;

/// <summary>
/// KPIs industriales agregados de un período (mes o año) y un alcance (planta, línea o máquina).
/// Guarda también las sumas y las pérdidas valoradas para que las pantallas no recalculen.
/// </summary>
public sealed class IndustrialKpiResult : Entity, ICompanyScoped
{
    public Guid CompanyId { get; set; }

    public int Year { get; set; }

    /// <summary>Mes (1–12); <c>null</c> en el agregado del año.</summary>
    public int? Month { get; set; }

    public IndustrialLevel Level { get; set; }

    /// <summary>Máquina (solo en el nivel <c>Machine</c>).</summary>
    public string? MachineId { get; set; }

    /// <summary>Línea (niveles <c>Line</c> y <c>Machine</c>).</summary>
    public string? LineId { get; set; }

    /// <summary>Período legible: <c>2024</c> o <c>2024-10</c>.</summary>
    public string Period => Month is { } m ? $"{Year:0000}-{m:00}" : $"{Year:0000}";

    // --- Sumas ---
    public decimal PlannedMinutes { get; set; }

    public decimal DowntimeMinutes { get; set; }

    public decimal TotalUnits { get; set; }

    public decimal GoodUnits { get; set; }

    public decimal UnitsProduced { get; set; }

    public int Days { get; set; }

    public decimal? LaborCost { get; set; }

    public decimal? MaterialCost { get; set; }

    public decimal? EnergyCost { get; set; }

    public decimal? OverheadCost { get; set; }

    public decimal? TotalCost { get; set; }

    // --- KPIs (ratios; null si no se pueden calcular) ---
    public decimal? Oee { get; set; }

    public decimal? Availability { get; set; }

    public decimal? Performance { get; set; }

    public decimal? Quality { get; set; }

    public decimal? ScrapRate { get; set; }

    public decimal? DowntimeRatio { get; set; }

    public decimal? CapacityUtilization { get; set; }

    /// <summary>Costo total / unidades producidas (monto).</summary>
    public decimal? CostPerUnit { get; set; }

    /// <summary>Costo estándar por unidad ponderado por las unidades producidas (monto).</summary>
    public decimal? StandardCostPerUnit { get; set; }

    // --- Pérdidas valoradas (montos) ---

    /// <summary>Minutos de parada × costo por minuto.</summary>
    public decimal? DowntimeCost { get; set; }

    /// <summary>(Unidades totales − buenas) × costo unitario.</summary>
    public decimal? ScrapCost { get; set; }

    /// <summary>Minutos perdidos por ir más lento que el estándar × costo por minuto.</summary>
    public decimal? SpeedLossCost { get; set; }

    public DateTime CalculatedAt { get; set; }
}
