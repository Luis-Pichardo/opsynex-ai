using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Opsynex.Infrastructure.Persistence.Converters;

/// <summary>Guarda fechas en UTC y las lee con <see cref="DateTimeKind.Utc"/>.</summary>
internal sealed class UtcDateTimeConverter()
    : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
