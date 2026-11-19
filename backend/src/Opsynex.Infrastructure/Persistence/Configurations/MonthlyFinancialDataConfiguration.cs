using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opsynex.Domain.Entities;

namespace Opsynex.Infrastructure.Persistence.Configurations;

internal sealed class MonthlyFinancialDataConfiguration : IEntityTypeConfiguration<MonthlyFinancialData>
{
    public void Configure(EntityTypeBuilder<MonthlyFinancialData> builder)
    {
        builder.ToTable("MonthlyFinancialData", t =>
        {
            t.HasCheckConstraint("CK_MonthlyFinancialData_Year", "[Year] BETWEEN 1900 AND 2200");
            t.HasCheckConstraint("CK_MonthlyFinancialData_Month", "[Month] BETWEEN 1 AND 12");
        });
        builder.ConfigureEntity();
        builder.BelongsToCompany();

        builder.HasOne<Upload>()
            .WithMany()
            .HasForeignKey(m => m.UploadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.CompanyId, m.Year, m.Month }).IsUnique();
    }
}
