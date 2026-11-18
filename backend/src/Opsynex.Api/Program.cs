using System.Globalization;
using Asp.Versioning;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Opsynex.Api.Extensions;
using Opsynex.Api.Health;
using Opsynex.Application;
using Opsynex.Infrastructure;
using Opsynex.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Serilog;

// Logger inicial para registrar errores de arranque; AddSerilog lo reemplaza al construir el host.
// (No se usa CreateBootstrapLogger porque falla cuando las pruebas crean varios hosts.)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Logging estructurado con Serilog (configurable desde la sección "Serilog").
    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddOpsynexProblemDetails();
    builder.Services.AddExceptionHandler<ConflictExceptionHandler>();
    builder.Services.AddFrontendCors(builder.Configuration);

    builder.Services.AddControllers();
    builder.Services
        .AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddMvc()
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        })
        .AddOpenApi(); // un documento OpenAPI por versión (/openapi/v1.json)

    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy("El API está en ejecución."), tags: ["live"])
        .AddDbContextCheck<OpsynexDbContext>("database", tags: ["ready"]);
    // Etapa 04: check del servicio de IA.

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    // En Development se aplican las migraciones pendientes al arrancar (Database:MigrateOnStartup).
    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        await app.Services.InitializeDatabaseAsync();
    }

    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().WithDocumentPerVersion();
        app.MapScalarApiReference(options => options.WithTitle("Opsynex AI API"));
    }

    app.UseCors(CorsExtensions.PolicyName);

    app.MapControllers();
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = HealthResponseWriter.WriteAsync,
    }).RequireCors(CorsExtensions.PolicyName);

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "El API terminó de forma inesperada");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Expuesto para las pruebas de integración con WebApplicationFactory.</summary>
public partial class Program;
