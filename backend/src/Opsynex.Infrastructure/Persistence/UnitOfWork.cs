using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Common.Exceptions;

namespace Opsynex.Infrastructure.Persistence;

/// <summary>Confirma los cambios y traduce las violaciones de restricciones únicas.</summary>
internal sealed class UnitOfWork(OpsynexDbContext context) : IUnitOfWork
{
    // SQL Server: 2601 (índice único) y 2627 (restricción UNIQUE/PK).
    private static readonly int[] SqlServerUniqueErrors = [2601, 2627];

    // SQLite: SQLITE_CONSTRAINT_UNIQUE (2067) y SQLITE_CONSTRAINT_PRIMARYKEY (1555).
    private static readonly int[] SqliteUniqueErrors = [2067, 1555];

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new UniqueConstraintViolationException(
                "Ya existe un registro con los mismos valores únicos (por ejemplo, la misma empresa y año).", ex);
        }
    }

    internal static bool IsUniqueViolation(DbUpdateException exception) => exception.InnerException switch
    {
        SqlException sql => SqlServerUniqueErrors.Contains(sql.Number),
        SqliteException sqlite => SqliteUniqueErrors.Contains(sqlite.SqliteExtendedErrorCode),
        _ => false,
    };
}
