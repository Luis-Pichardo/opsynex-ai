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
        builder.HasIndex(r => new { r.CompanyId, r.Year });
        builder.HasIndex(r => r.UploadId);
    }
}
