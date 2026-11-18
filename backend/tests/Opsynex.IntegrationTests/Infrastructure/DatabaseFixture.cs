using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Opsynex.Application.Abstractions.Tenancy;
using Opsynex.Infrastructure;
using Opsynex.Infrastructure.Persistence;
using Opsynex.Infrastructure.Persistence.Interceptors;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(Opsynex.IntegrationTests.Infrastructure.DatabaseFixture))]

namespace Opsynex.IntegrationTests.Infrastructure;

/// <summary>
/// Base de datos compartida por todas las pruebas de integración.
/// <list type="bullet">
/// <item>Con Docker: SQL Server 2022 real en un contenedor (Testcontainers) con las migraciones aplicadas.</item>
/// <item>Sin Docker: SQLite in-memory (esquema creado desde el modelo).</item>
/// </list>
/// Variable <c>OPSYNEX_TEST_DB</c>: <c>sqlserver</c> exige Docker; <c>sqlite</c> fuerza SQLite.
/// Las pruebas crean empresas nuevas, así que no interfieren entre sí.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest";

    private MsSqlContainer? _container;
    private SqliteConnection? _sqliteKeepAlive;

    public DatabaseProvider Provider { get; private set; }

    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Motivo por el que se usó SQLite (si aplica).</summary>
    public string? FallbackReason { get; private set; }

    public bool IsSqlServer => Provider == DatabaseProvider.SqlServer;

    public async ValueTask InitializeAsync()
    {
        var mode = Environment.GetEnvironmentVariable("OPSYNEX_TEST_DB")?.Trim().ToUpperInvariant();

        if (mode != "SQLITE")
        {
            try
            {
                _container = new MsSqlBuilder(SqlServerImage).Build();
                await _container.StartAsync();
                Provider = DatabaseProvider.SqlServer;
                ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
                {
                    InitialCatalog = "OpsynexTests",
                }.ConnectionString;
            }
            catch (Exception ex) when (mode != "SQLSERVER")
            {
                FallbackReason = ex.Message;
                if (_container is not null)
                {
                    await _container.DisposeAsync();
                    _container = null;
                }
            }
        }

        if (_container is null)
        {
            Provider = DatabaseProvider.Sqlite;
            ConnectionString = $"Data Source=opsynex-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            // La BD en memoria vive mientras haya una conexión abierta.
            _sqliteKeepAlive = new SqliteConnection(ConnectionString);
            await _sqliteKeepAlive.OpenAsync();
        }

        await using var context = CreateContext();
        if (IsSqlServer)
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }
    }

    /// <summary>Crea un contexto nuevo, opcionalmente con un tenant (filtro por empresa).</summary>
    public OpsynexDbContext CreateContext(ICurrentTenant? tenant = null, TimeProvider? timeProvider = null)
    {
        var builder = new DbContextOptionsBuilder<OpsynexDbContext>()
            .AddInterceptors(new AuditableEntityInterceptor(timeProvider ?? TimeProvider.System));

        if (IsSqlServer)
        {
            builder.UseSqlServer(ConnectionString, DependencyInjection.ConfigureSqlServer);
        }
        else
        {
            builder.UseSqlite(ConnectionString);
        }

        return new OpsynexDbContext(builder.Options, tenant);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }

        if (_sqliteKeepAlive is not null)
        {
            await _sqliteKeepAlive.DisposeAsync();
        }
    }
}
