using Opsynex.Domain.Common;

namespace Opsynex.Domain.Entities;

/// <summary>
/// Registro de producción de una máquina en un día (módulo industrial). Los tiempos van en
/// minutos y el tiempo de ciclo en segundos por unidad.
/// </summary>
public sealed class ProductionData : Entity, ICompanyScoped
{
    public Guid CompanyId { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>Código de la máquina (p. ej. <c>MAQ-01</c>).</summary>
    public required string MachineId { get; set; }

    /// <summary>Código de la línea de producción (opcional).</summary>
    public string? LineId { get; set; }

    /// <summary>Unidades terminadas y entregadas (si no se indica, son las unidades buenas).</summary>
    public decimal UnitsProduced { get; set; }

    /// <summary>Minutos de parada dentro del tiempo planificado.</summary>
    public decimal DowntimeMinutes { get; set; }

    /// <summary>Minutos planificados de producción.</summary>
    public decimal PlannedMinutes { get; set; }

    /// <summary>Tiempo de ciclo real promedio (segundos por unidad), informativo.</summary>
    public decimal? CycleTime { get; set; }

    /// <summary>Unidades buenas (sin defectos).</summary>
    public decimal GoodUnits { get; set; }

    /// <summary>Unidades procesadas en total (buenas + scrap).</summary>
    public decimal TotalUnits { get; set; }

    public Guid? UploadId { get; set; }
}

/// <summary>Costos de una máquina en una fecha (diaria o de cierre de mes).</summary>
public sealed class CostData : Entity, ICompanyScoped
{
    public Guid CompanyId { get; set; }

    public DateOnly Date { get; set; }

    public required string MachineId { get; set; }

    public decimal LaborCost { get; set; }

    public decimal MaterialCost { get; set; }

    public decimal EnergyCost { get; set; }

    public decimal OverheadCost { get; set; }

    public Guid? UploadId { get; set; }

    public decimal TotalCost => LaborCost + MaterialCost + EnergyCost + OverheadCost;
}

/// <summary>Estándar de operación de una máquina o proceso.</summary>
public sealed class StandardData : Entity, ICompanyScoped
{
    public Guid CompanyId { get; set; }

    /// <summary>Máquina a la que aplica el estándar.</summary>
    public required string MachineId { get; set; }

    /// <summary>Nombre del proceso (descriptivo).</summary>
    public string? ProcessName { get; set; }

    /// <summary>Tiempo de ciclo estándar (segundos por unidad).</summary>
    public decimal StandardCycleTime { get; set; }

    /// <summary>Costo estándar por unidad.</summary>
    public decimal? StandardCost { get; set; }

    /// <summary>Costo de un minuto de máquina (para valorar las paradas).</summary>
    public decimal? CostPerMinute { get; set; }

    public Guid? UploadId { get; set; }
}
