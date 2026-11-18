using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Opsynex.Domain.Entities;
using Opsynex.Infrastructure.Persistence.Converters;

namespace Opsynex.Infrastructure.Persistence.Configurations;

internal sealed class UploadConfiguration : IEntityTypeConfiguration<Upload>
{
    public void Configure(EntityTypeBuilder<Upload> builder)
    {
        builder.ToTable("Uploads", t =>
        {
            t.HasCheckConstraint("CK_Uploads_Errors_Json", "ISJSON([Errors]) = 1");
            t.HasCheckConstraint("CK_Uploads_Warnings_Json", "ISJSON([Warnings]) = 1");
            t.HasCheckConstraint("CK_Uploads_Sheets_Json", "ISJSON([Sheets]) = 1");
            t.HasCheckConstraint("CK_Uploads_Years_Json", "ISJSON([Years]) = 1");
            t.HasCheckConstraint("CK_Uploads_Summary_Json", "ISJSON([Summary]) = 1");
        });
        builder.ConfigureEntity();
        builder.BelongsToCompany();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(u => u.FileName).HasMaxLength(260).IsRequired();
        builder.Property(u => u.Errors).HasJsonListConversion();
        builder.Property(u => u.Warnings).HasJsonListConversion();
        builder.Property(u => u.Sheets).HasJsonListConversion();
        builder.Property(u => u.Years).HasJsonListConversion();
        builder.Property(u => u.Summary).HasJsonValueConversion();

        // Al borrar una carga se borran sus valores crudos.
        builder.HasMany(u => u.RawValues)
            .WithOne()
            .HasForeignKey(r => r.UploadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(u => new { u.CompanyId, u.CreatedAt });
    }
}
