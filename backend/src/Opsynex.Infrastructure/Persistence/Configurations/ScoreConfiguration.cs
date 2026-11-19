using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opsynex.Domain.Entities;
using Opsynex.Infrastructure.Persistence.Converters;

namespace Opsynex.Infrastructure.Persistence.Configurations;

internal sealed class ScoreConfiguration : IEntityTypeConfiguration<Score>
{
    public void Configure(EntityTypeBuilder<Score> builder)
    {
        builder.ToTable("Scores", t =>
        {
            t.HasCheckConstraint("CK_Scores_WeightsUsed_Json", "ISJSON([WeightsUsed]) = 1");
            t.HasCheckConstraint("CK_Scores_Components_Json", "ISJSON([Components]) = 1");
            t.HasCheckConstraint("CK_Scores_TotalScore", "[TotalScore] BETWEEN 0 AND 100");
        });
        builder.ConfigureCompanyYear();

        builder.Property(s => s.TotalScore).HasPrecision(5, 2);
        builder.Property(s => s.ProfitabilityScore).HasPrecision(5, 2);
        builder.Property(s => s.CostEfficiencyScore).HasPrecision(5, 2);
        builder.Property(s => s.ProductivityScore).HasPrecision(5, 2);
        builder.Property(s => s.FinancialStabilityScore).HasPrecision(5, 2);
        builder.Property(s => s.WeightsUsed).HasJsonDictionaryConversion();
        builder.Property(s => s.Components).HasJsonListConversion();
    }
}
