using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opsynex.Domain.Entities;

namespace Opsynex.Infrastructure.Persistence.Configurations;

internal sealed class ProductionDataConfiguration : IEntityTypeConfiguration<ProductionData>
{
    public void Configure(EntityTypeBuilder<ProductionData> builder)
    {
        builder.ToTable("ProductionData", t =>
        {
            t.HasCheckConstraint("CK_ProductionData_Minutes", "[PlannedMinutes] > 0 AND [DowntimeMinutes] >= 0 AND [DowntimeMinutes] <= [PlannedMinutes]");
            t.HasCheckConstraint("CK_ProductionData_Units", "[GoodUnits] >= 0 AND [GoodUnits] <= [TotalUnits] AND [UnitsProduced] >= 0");
        });
        builder.ConfigureEntity();
        builder.BelongsToCompany();
        builder.Property(p => p.MachineId).HasMaxLength(50).IsRequired();
        builder.Property(p => p.LineId).HasMaxLength(50);
        builder.HasOne<Upload>().WithMany().HasForeignKey(p => p.UploadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.CompanyId, p.Date, p.MachineId }).IsUnique();
    }
}

internal sealed class CostDataConfiguration : IEntityTypeConfiguration<CostData>
{
    public void Configure(EntityTypeBuilder<CostData> builder)
    {
        builder.ToTable("CostData", t =>
            t.HasCheckConstraint("CK_CostData_NonNegative", "[LaborCost] >= 0 AND [MaterialCost] >= 0 AND [EnergyCost] >= 0 AND [OverheadCost] >= 0"));
        builder.ConfigureEntity();
        builder.BelongsToCompany();
        builder.Property(c => c.MachineId).HasMaxLength(50).IsRequired();
        builder.Ignore(c => c.TotalCost);
        builder.HasOne<Upload>().WithMany().HasForeignKey(c => c.UploadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.CompanyId, c.Date, c.MachineId }).IsUnique();
    }
}

internal sealed class StandardDataConfiguration : IEntityTypeConfiguration<StandardData>
{
    public void Configure(EntityTypeBuilder<StandardData> builder)
    {
        builder.ToTable("StandardData", t =>
            t.HasCheckConstraint("CK_StandardData_CycleTime", "[StandardCycleTime] > 0"));
        builder.ConfigureEntity();
        builder.BelongsToCompany();
        builder.Property(s => s.MachineId).HasMaxLength(50).IsRequired();
        builder.Property(s => s.ProcessName).HasMaxLength(200);
        builder.Property(s => s.StandardCycleTime).HasPrecision(18, 4);
        builder.Property(s => s.StandardCost).HasPrecision(18, 4);
        builder.Property(s => s.CostPerMinute).HasPrecision(18, 4);
        builder.HasOne<Upload>().WithMany().HasForeignKey(s => s.UploadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.CompanyId, s.MachineId }).IsUnique();
    }
}

internal sealed class IndustrialKpiResultConfiguration : IEntityTypeConfiguration<IndustrialKpiResult>
{
    public void Configure(EntityTypeBuilder<IndustrialKpiResult> builder)
    {
        builder.ToTable("IndustrialKpiResults", t =>
        {
            t.HasCheckConstraint("CK_IndustrialKpiResult_Year", "[Year] BETWEEN 1900 AND 2200");
            t.HasCheckConstraint("CK_IndustrialKpiResult_Month", "[Month] IS NULL OR [Month] BETWEEN 1 AND 12");
        });
        builder.ConfigureEntity();
        builder.BelongsToCompany();
        builder.Ignore(k => k.Period);
        builder.Property(k => k.MachineId).HasMaxLength(50);
        builder.Property(k => k.LineId).HasMaxLength(50);

        builder.Property(k => k.Oee).IsRatio();
        builder.Property(k => k.Availability).IsRatio();
        builder.Property(k => k.Performance).IsRatio();
        builder.Property(k => k.Quality).IsRatio();
        builder.Property(k => k.ScrapRate).IsRatio();
        builder.Property(k => k.DowntimeRatio).IsRatio();
        builder.Property(k => k.CapacityUtilization).IsRatio();
        builder.Property(k => k.StandardCostPerUnit).HasPrecision(18, 4);

        builder.HasIndex(k => new { k.CompanyId, k.Year, k.Month, k.Level });
    }
}
