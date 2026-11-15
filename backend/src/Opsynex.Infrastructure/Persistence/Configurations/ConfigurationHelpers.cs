using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opsynex.Domain.Common;
using Opsynex.Domain.Entities;

namespace Opsynex.Infrastructure.Persistence.Configurations;

internal static class ConfigurationHelpers
{
    public const int RatioPrecision = 9;
    public const int RatioScale = 4;

    /// <summary>Clave primaria y columnas de auditoría.</summary>
    public static void ConfigureEntity<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : Entity
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
    }

    /// <summary>FK obligatoria a <see cref="Company"/> sin borrado en cascada.</summary>
    public static void BelongsToCompany<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : Entity, ICompanyScoped
    {
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>Dato anual por empresa: FK a Company e índice único (CompanyId, Year).</summary>
    public static void ConfigureCompanyYear<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : Entity, ICompanyYearScoped
    {
        builder.ConfigureEntity();
        builder.BelongsToCompany();
        builder.HasIndex(e => new { e.CompanyId, e.Year }).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint($"CK_{typeof(TEntity).Name}_Year", "[Year] BETWEEN 1900 AND 2200"));
    }

    /// <summary>Ratio con precisión decimal(9,4).</summary>
    public static PropertyBuilder<decimal?> IsRatio(this PropertyBuilder<decimal?> property) =>
        property.HasPrecision(RatioPrecision, RatioScale);
}
