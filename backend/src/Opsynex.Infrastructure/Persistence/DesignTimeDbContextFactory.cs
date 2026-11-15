using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Opsynex.Infrastructure.Persistence;

/// <summary>
/// Usado por <c>dotnet ef</c> (migraciones). Lee la cadena de <c>ConnectionStrings__Default</c>
/// o usa la del SQL Server local de docker-compose.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OpsynexDbContext>
{
    private const string LocalConnection =
        "Server=localhost,1433;Database=Opsynex;User Id=sa;Password=Opsynex_Dev_2026!;TrustServerCertificate=True";

    public OpsynexDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        var options = new DbContextOptionsBuilder<OpsynexDbContext>()
            .UseSqlServer(string.IsNullOrWhiteSpace(connection) ? LocalConnection : connection, DependencyInjection.ConfigureSqlServer)
            .Options;
        return new OpsynexDbContext(options);
    }
}
