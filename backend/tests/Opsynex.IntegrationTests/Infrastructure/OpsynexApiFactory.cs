using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Opsynex.IntegrationTests.Infrastructure;

/// <summary>
/// Levanta el API en memoria apuntando a la base de datos de pruebas (<see cref="DatabaseFixture"/>).
/// Incluye los controladores de prueba de este ensamblado (matriz de permisos).
/// </summary>
public class OpsynexApiFactory(DatabaseFixture database) : WebApplicationFactory<Program>
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
        builder.UseSetting("Jwt:Secret", TestJwt.Secret);
        // Por defecto sin servicio de IA (fallback local inmediato); ver AiApiFactory.
        builder.UseSetting("AiService:Enabled", "false");
        // Sin IA generativa salvo en las pruebas de IA (que usan un cliente simulado y modo Inline).
        builder.UseSetting("Ai:Enabled", "false");
        ConfigureSettings(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(OpsynexApiFactory).Assembly);
            ConfigureServices(services);
        });
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    protected virtual void ConfigureSettings(IWebHostBuilder builder)
    {
    }
}

/// <summary>API con un límite de carga de 1 MB (para probar el 413).</summary>
public sealed class SmallUploadLimitApiFactory(DatabaseFixture database) : OpsynexApiFactory(database)
{
    protected override void ConfigureSettings(IWebHostBuilder builder) => builder.UseSetting("Upload:MaxMb", "1");
}

/// <summary>API con un reloj controlable (bloqueos y expiración de tokens).</summary>
public sealed class TimeTravelApiFactory(DatabaseFixture database) : OpsynexApiFactory(database)
{
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<TimeProvider>(Clock);
}

public static class TestJwt
{
    public const string Secret = "secreto-de-pruebas-de-integracion-0123456789-abcdef";
}

/// <summary>API conectado al servicio de IA real (<see cref="AiServiceFixture"/>).</summary>
public sealed class AiApiFactory(DatabaseFixture database, AiServiceFixture ai) : OpsynexApiFactory(database)
{
    public AiServiceFixture Ai { get; } = ai;

    protected override void ConfigureSettings(IWebHostBuilder builder)
    {
        builder.UseSetting("AiService:Enabled", "true");
        builder.UseSetting("AiService:BaseUrl", Ai.BaseUrl ?? "http://127.0.0.1:9");
        builder.UseSetting("AiService:ApiKey", Ai.ApiKey);
    }
}

/// <summary>API con el servicio de IA activado pero inalcanzable (prueba del fallback y la resiliencia).</summary>
public sealed class UnreachableAiApiFactory(DatabaseFixture database) : OpsynexApiFactory(database)
{
    protected override void ConfigureSettings(IWebHostBuilder builder)
    {
        builder.UseSetting("AiService:Enabled", "true");
        builder.UseSetting("AiService:BaseUrl", "http://127.0.0.1:9");
        builder.UseSetting("AiService:TimeoutSeconds", "3");
        builder.UseSetting("AiService:MaxRetries", "1");
    }
}
