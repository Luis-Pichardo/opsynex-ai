using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opsynex.Domain.Entities;

namespace Opsynex.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.ConfigureEntity();
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Sector).HasMaxLength(100);
        builder.Property(c => c.Currency).HasMaxLength(3).IsFixedLength().IsUnicode(false).HasDefaultValue("USD").IsRequired();
        builder.HasIndex(c => c.Name);
    }
}
