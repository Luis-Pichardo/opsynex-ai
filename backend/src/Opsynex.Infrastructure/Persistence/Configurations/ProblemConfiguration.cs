using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opsynex.Domain.Entities;
using Opsynex.Infrastructure.Persistence.Converters;

namespace Opsynex.Infrastructure.Persistence.Configurations;

internal sealed class ProblemConfiguration : IEntityTypeConfiguration<Problem>
{
    public void Configure(EntityTypeBuilder<Problem> builder)
    {
        builder.ToTable("Problems", t =>
        {
            t.HasCheckConstraint("CK_Problems_Evidence_Json", "ISJSON([Evidence]) = 1");
            t.HasCheckConstraint("CK_Problem_Year", "[Year] BETWEEN 1900 AND 2200");
        });
        builder.ConfigureEntity();
        builder.BelongsToCompany();

        builder.Property(p => p.Code).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(4000).IsRequired();
        builder.Property(p => p.Recommendation).HasMaxLength(4000).IsRequired();
        builder.Property(p => p.AiRecommendation);
        builder.Property(p => p.Evidence).HasJsonObjectConversion();

        builder.HasIndex(p => new { p.CompanyId, p.Year });
        // Una regla genera a lo sumo un problema por empresa y año (re-ejecución idempotente).
        builder.HasIndex(p => new { p.CompanyId, p.Year, p.Code }).IsUnique();
    }
}
