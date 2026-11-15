using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opsynex.Domain.Entities;

namespace Opsynex.Infrastructure.Persistence.Configurations;

internal sealed class KpiResultConfiguration : IEntityTypeConfiguration<KpiResult>
{
    public void Configure(EntityTypeBuilder<KpiResult> builder)
    {
        builder.ToTable("KpiResults");
        builder.ConfigureCompanyYear();

        builder.Property(k => k.EbitdaMargin).IsRatio();
        builder.Property(k => k.GrossMargin).IsRatio();
        builder.Property(k => k.CostEfficiency).IsRatio();
        builder.Property(k => k.OperatingCostRatio).IsRatio();
        // RevenuePerEmployee es un monto: decimal(18,2).
    }
}
