using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opsynex.Domain.Entities;

namespace Opsynex.Infrastructure.Persistence.Configurations;

internal sealed class FinancialDataConfiguration : IEntityTypeConfiguration<FinancialData>
{
    public void Configure(EntityTypeBuilder<FinancialData> builder)
    {
        // Los montos usan la convención decimal(18,2) del DbContext.
        builder.ToTable("FinancialData");
        builder.ConfigureCompanyYear();
    }
}
