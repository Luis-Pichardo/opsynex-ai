using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opsynex.Domain.Entities;
using Opsynex.Infrastructure.Persistence.Converters;

namespace Opsynex.Infrastructure.Persistence.Configurations;

internal sealed class ExecutiveSummaryConfiguration : IEntityTypeConfiguration<ExecutiveSummary>
{
    public void Configure(EntityTypeBuilder<ExecutiveSummary> builder)
    {
        builder.ToTable("ExecutiveSummaries", t =>
        {
            t.HasCheckConstraint("CK_ExecutiveSummaries_Paragraphs_Json", "ISJSON([Paragraphs]) = 1");
            t.HasCheckConstraint("CK_ExecutiveSummaries_Priorities_Json", "ISJSON([Priorities]) = 1");
        });
        builder.ConfigureCompanyYear();

        builder.Property(s => s.Paragraphs).HasJsonListConversion();
        builder.Property(s => s.Priorities).HasJsonListConversion();
        builder.Property(s => s.Model).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(s => s.InputHash).HasMaxLength(64).IsUnicode(false).IsRequired();
    }
}
