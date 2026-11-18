using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opsynex.Domain.Entities;

namespace Opsynex.Infrastructure.Persistence.Configurations;

internal sealed class AccountMappingConfiguration : IEntityTypeConfiguration<AccountMapping>
{
    public void Configure(EntityTypeBuilder<AccountMapping> builder)
    {
        builder.ToTable("AccountMappings", t =>
            t.HasCheckConstraint("CK_AccountMappings_Confidence", "[Confidence] BETWEEN 0 AND 1"));
        builder.ConfigureEntity();
        builder.BelongsToCompany();

        builder.Property(m => m.ClientAccount).HasMaxLength(300).IsRequired();
        builder.Property(m => m.Confidence).HasPrecision(5, 4);
        builder.Ignore(m => m.IsConfirmed);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.ConfirmedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.CompanyId, m.ClientAccount }).IsUnique();
    }
}
