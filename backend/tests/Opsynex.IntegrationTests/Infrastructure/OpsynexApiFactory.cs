using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Opsynex.IntegrationTests.Infrastructure;

/// <summary>
/// Levanta el API en memoria apuntando a la base de datos de pruebas (<see cref="DatabaseFixture"/>).
/// </summary>
public sealed class OpsynexApiFactory(DatabaseFixture database) : WebApplicationFactory<Program>
{
    public DatabaseFixture Database { get; } = database;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Cors:Origins:0", "http://localhost:5173");
        builder.UseSetting("ConnectionStrings:Default", Database.ConnectionString);
        builder.UseSetting("Database:Provider", Database.Provider.ToString());
        // El esquema ya lo creó DatabaseFixture.
        builder.UseSetting("Database:MigrateOnStartup", "false");
    }
}
