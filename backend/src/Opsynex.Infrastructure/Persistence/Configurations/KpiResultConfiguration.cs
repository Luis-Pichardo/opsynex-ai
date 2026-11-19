using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opsynex.Domain.Entities;
using Opsynex.Infrastructure.Persistence.Converters;

namespace Opsynex.Infrastructure.Persistence.Configurations;

internal sealed class KpiResultConfiguration : IEntityTypeConfiguration<KpiResult>
{
    public void Configure(EntityTypeBuilder<KpiResult> builder)
    {
        builder.ToTable("KpiResults", t =>
        {
            t.HasCheckConstraint("CK_KpiResults_Trends_Json", "ISJSON([Trends]) = 1");
            t.HasCheckConstraint("CK_KpiResults_Normalized_Json", "ISJSON([Normalized]) = 1");
            t.HasCheckConstraint("CK_KpiResults_Highlights_Json", "ISJSON([Highlights]) = 1");
        });
        builder.ConfigureCompanyYear();

        // Ratios: decimal(9,4). Montos (ingresos por empleado, costo por unidad): decimal(18,2).
        builder.Property(k => k.EbitdaMargin).IsRatio();
        builder.Property(k => k.GrossMargin).IsRatio();
        builder.Property(k => k.CostEfficiency).IsRatio();
        builder.Property(k => k.OperatingCostRatio).IsRatio();
        builder.Property(k => k.OverheadRatio).IsRatio();
        builder.Property(k => k.EnergyCostRatio).IsRatio();
        builder.Property(k => k.NetMargin).IsRatio();
        builder.Property(k => k.LaborCostRatio).IsRatio();
        builder.Property(k => k.CurrentRatio).IsRatio();
        builder.Property(k => k.DebtToEquity).IsRatio();
        // Días: decimal(9,2).
        builder.Property(k => k.DaysSalesOutstanding).HasPrecision(9, 2);
        builder.Property(k => k.InventoryDays).HasPrecision(9, 2);

        builder.Property(k => k.Trends).HasJsonValueConversion();
        builder.Property(k => k.Normalized).HasJsonDictionaryConversion();
        builder.Property(k => k.Highlights).HasJsonListConversion();
    }
}
