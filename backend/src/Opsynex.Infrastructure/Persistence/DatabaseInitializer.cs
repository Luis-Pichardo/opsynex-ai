using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Opsynex.Infrastructure.Persistence;

/// <summary>Crea o actualiza el esquema de la base de datos al arrancar.</summary>
public static partial class DatabaseInitializer
{
    /// <summary>
    /// SQL Server: aplica las migraciones pendientes. SQLite (pruebas): crea el esquema desde el modelo.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var context = scope.ServiceProvider.GetRequiredService<OpsynexDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        if (options.Provider == DatabaseProvider.Sqlite)
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
            LogSchemaCreated(logger);
            return;
        }

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            LogApplyingMigrations(logger, pending.Count, pending);
            await context.Database.MigrateAsync(cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Aplicando {Count} migraciones: {Migrations}")]
    private static partial void LogApplyingMigrations(ILogger logger, int count, IReadOnlyList<string> migrations);

    [LoggerMessage(Level = LogLevel.Information, Message = "Esquema SQLite creado desde el modelo")]
    private static partial void LogSchemaCreated(ILogger logger);
}
