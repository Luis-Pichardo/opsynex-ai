using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Opsynex.IntegrationTests;

/// <summary>
/// Levanta el API en memoria para las pruebas de integración.
/// Etapa 01: aquí se configurará la BD (Testcontainers con SQL Server si hay Docker; si no, SQLite in-memory).
/// </summary>
public sealed class OpsynexApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Cors:Origins:0", "http://localhost:5173");
    }
}
