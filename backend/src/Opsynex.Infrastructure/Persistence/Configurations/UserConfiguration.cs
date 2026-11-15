using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opsynex.Domain.Entities;

namespace Opsynex.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.ConfigureEntity();

#pragma warning disable CA1308 // Los correos se normalizan en minúsculas por convención.
        builder.Property(u => u.Email)
            .HasMaxLength(256)
            .IsRequired()
            .HasConversion(v => v.Trim().ToLowerInvariant(), v => v);
#pragma warning restore CA1308
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.IsActive).HasDefaultValue(true);

        // Admin global sin empresa: CompanyId opcional.
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(u => u.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(u => u.CompanyId);
    }
}
