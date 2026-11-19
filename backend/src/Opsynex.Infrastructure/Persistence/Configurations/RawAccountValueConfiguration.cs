using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opsynex.Domain.Entities;

namespace Opsynex.Infrastructure.Persistence.Configurations;

internal sealed class RawAccountValueConfiguration : IEntityTypeConfiguration<RawAccountValue>
{
    public void Configure(EntityTypeBuilder<RawAccountValue> builder)
    {
        builder.ToTable("RawAccountValues", t => t.HasCheckConstraint("CK_RawAccountValue_Year", "[Year] BETWEEN 1900 AND 2200"));
        builder.ConfigureEntity();
        builder.BelongsToCompany();
        builder.Property(r => r.ClientAccount).HasMaxLength(300).IsRequired();
        builder.Property(r => r.SheetName).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Section).HasMaxLength(300);
        builder.Property(r => r.OriginalLabel).HasMaxLength(500).IsRequired();
        builder.Property(r => r.NormalizedLabel).HasMaxLength(300).IsRequired();
        builder.Property(r => r.AccountCode).HasMaxLength(32).IsUnicode(false);
        builder.HasIndex(r => new { r.CompanyId, r.Year });
        builder.HasIndex(r => new { r.UploadId, r.SheetType });
    }
}
