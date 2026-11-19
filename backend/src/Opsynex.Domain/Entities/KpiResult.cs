using Opsynex.Domain.Common;
using Opsynex.Domain.Engines.Kpis;

namespace Opsynex.Domain.Entities;

/// <summary>KPIs calculados de una empresa para un año. Un KPI es <c>null</c> si no se puede calcular.</summary>
public sealed class KpiResult : Entity, ICompanyYearScoped
{
    public Guid CompanyId { get; set; }

    public int Year { get; set; }

    /// <summary>EBITDA / Revenue.</summary>
    public decimal? EbitdaMargin { get; set; }

    /// <summary>(Revenue − COGS) / Revenue.</summary>
    public decimal? GrossMargin { get; set; }

    /// <summary>Total cost / Revenue.</summary>
    public decimal? CostEfficiency { get; set; }

    /// <summary>Revenue / número de empleados (monto).</summary>
    public decimal? RevenuePerEmployee { get; set; }

    /// <summary>Operating expenses / Revenue.</summary>
    public decimal? OperatingCostRatio { get; set; }

    /// <summary>Overhead cost / Revenue.</summary>
    public decimal? OverheadRatio { get; set; }

    /// <summary>Energy cost / Revenue.</summary>
    public decimal? EnergyCostRatio { get; set; }

    /// <summary>Total cost / unidades producidas (monto).</summary>
    public decimal? CostPerUnit { get; set; }

    /// <summary>Net income / Revenue.</summary>
    public decimal? NetMargin { get; set; }

    /// <summary>Labor cost / Revenue.</summary>
    public decimal? LaborCostRatio { get; set; }

    /// <summary>Current assets / Current liabilities.</summary>
    public decimal? CurrentRatio { get; set; }

    /// <summary>Total liabilities / Equity.</summary>
    public decimal? DebtToEquity { get; set; }

    /// <summary>Accounts receivable / Revenue × 365.</summary>
    public decimal? DaysSalesOutstanding { get; set; }

    /// <summary>Inventory / COGS × 365.</summary>
    public decimal? InventoryDays { get; set; }

    /// <summary>Tendencias frente al año anterior (columna JSON; vacía en el primer año).</summary>
    public YearTrends Trends { get; set; } = new();

    /// <summary>KPIs normalizados a 0–100 con los benchmarks de la industria y la moneda (columna JSON).</summary>
    public IDictionary<string, decimal?> Normalized { get; set; } = new Dictionary<string, decimal?>();

    /// <summary>Hallazgos positivos del diagnóstico del año (columna JSON).</summary>
    public IList<Engines.Diagnosis.Highlight> Highlights { get; set; } = [];

    public DateTime CalculatedAt { get; set; }

    /// <summary>Valor de un KPI por su código.</summary>
    public decimal? Get(KpiCode code) => code switch
    {
        KpiCode.EbitdaMargin => EbitdaMargin,
        KpiCode.GrossMargin => GrossMargin,
        KpiCode.CostEfficiency => CostEfficiency,
        KpiCode.RevenuePerEmployee => RevenuePerEmployee,
        KpiCode.OperatingCostRatio => OperatingCostRatio,
        KpiCode.OverheadRatio => OverheadRatio,
        KpiCode.EnergyCostRatio => EnergyCostRatio,
        KpiCode.CostPerUnit => CostPerUnit,
        KpiCode.NetMargin => NetMargin,
        KpiCode.LaborCostRatio => LaborCostRatio,
        KpiCode.CurrentRatio => CurrentRatio,
        KpiCode.DebtToEquity => DebtToEquity,
        KpiCode.DaysSalesOutstanding => DaysSalesOutstanding,
        KpiCode.InventoryDays => InventoryDays,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    /// <summary>Todos los KPIs como diccionario.</summary>
    public IReadOnlyDictionary<KpiCode, decimal?> Values() => KpiCatalog.All.ToDictionary(d => d.Code, d => Get(d.Code));

    /// <summary>Asigna los valores calculados por el motor.</summary>
    public void SetValues(IReadOnlyDictionary<KpiCode, decimal?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        EbitdaMargin = values.GetValueOrDefault(KpiCode.EbitdaMargin);
        GrossMargin = values.GetValueOrDefault(KpiCode.GrossMargin);
        CostEfficiency = values.GetValueOrDefault(KpiCode.CostEfficiency);
        RevenuePerEmployee = values.GetValueOrDefault(KpiCode.RevenuePerEmployee);
        OperatingCostRatio = values.GetValueOrDefault(KpiCode.OperatingCostRatio);
        OverheadRatio = values.GetValueOrDefault(KpiCode.OverheadRatio);
        EnergyCostRatio = values.GetValueOrDefault(KpiCode.EnergyCostRatio);
        CostPerUnit = values.GetValueOrDefault(KpiCode.CostPerUnit);
        NetMargin = values.GetValueOrDefault(KpiCode.NetMargin);
        LaborCostRatio = values.GetValueOrDefault(KpiCode.LaborCostRatio);
        CurrentRatio = values.GetValueOrDefault(KpiCode.CurrentRatio);
        DebtToEquity = values.GetValueOrDefault(KpiCode.DebtToEquity);
        DaysSalesOutstanding = values.GetValueOrDefault(KpiCode.DaysSalesOutstanding);
        InventoryDays = values.GetValueOrDefault(KpiCode.InventoryDays);
    }
}
