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
        catch (DbUpdateException ex) when (IsReferenceViolation(ex))
        {
            throw new ReferenceConstraintViolationException(
                "El registro está en uso por otros datos y no se puede eliminar.", ex);
        }
    }

    // SQL Server 547 (REFERENCE): borrado bloqueado por una FOREIGN KEY.
    // SQLite 787 (SQLITE_CONSTRAINT_FOREIGNKEY) y 1811 (ON DELETE RESTRICT, SQLITE_CONSTRAINT_TRIGGER).
    private static readonly int[] SqliteReferenceErrors = [787, 1811];

    internal static bool IsReferenceViolation(DbUpdateException exception) => exception.InnerException switch
    {
        SqlException sql => sql.Number == 547 && sql.Message.Contains("REFERENCE", StringComparison.OrdinalIgnoreCase),
        SqliteException sqlite => SqliteReferenceErrors.Contains(sqlite.SqliteExtendedErrorCode),
        _ => false,
    };

    internal static bool IsUniqueViolation(DbUpdateException exception) => exception.InnerException switch
    {
        SqlException sql => SqlServerUniqueErrors.Contains(sql.Number),
        SqliteException sqlite => SqliteUniqueErrors.Contains(sqlite.SqliteExtendedErrorCode),
        _ => false,
    };
}
