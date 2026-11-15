using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opsynex.Domain.Entities;

namespace Opsynex.Infrastructure.Persistence.Configurations;

internal sealed class OperationalDataConfiguration : IEntityTypeConfiguration<OperationalData>
{
    public void Configure(EntityTypeBuilder<OperationalData> builder)
    {
        builder.ToTable("OperationalData");
        builder.ConfigureCompanyYear();

        builder.HasOne<Upload>()
            .WithMany()
            .HasForeignKey(o => o.UploadId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
