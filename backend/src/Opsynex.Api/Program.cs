using System.Globalization;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Opsynex.Api.Auth;
using Opsynex.Api.Extensions;
using Opsynex.Api.OpenApi;
using Opsynex.Api.Health;
using Opsynex.Application;
using Opsynex.Application.Abstractions.Tenancy;
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
    // Comando "seed-admin": crea el primer Admin desde Seed:AdminEmail/Seed:AdminPassword y termina.
    const string SeedAdminCommand = "seed-admin";
    var runSeedCommand = args.Contains(SeedAdminCommand, StringComparer.OrdinalIgnoreCase);
    var builder = WebApplication.CreateBuilder(
        args.Where(a => !a.Equals(SeedAdminCommand, StringComparison.OrdinalIgnoreCase)).ToArray());

    // Logging estructurado con Serilog (configurable desde la sección "Serilog").
    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddOpsynexProblemDetails();

    // Límite del cuerpo de la petición: Upload:MaxMb + 1 MB de margen para el formulario.
    var maxUploadBytes = (builder.Configuration.GetValue<int?>("Upload:MaxMb") ?? 20) * 1024L * 1024L + 1024L * 1024L;
    builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = maxUploadBytes);
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = maxUploadBytes);
    builder.Services.AddExceptionHandler<AppExceptionHandler>();
    builder.Services.AddFrontendCors(builder.Configuration);
    builder.Services.AddOpsynexSecurity(builder.Configuration);

    builder.Services.AddControllers()
        .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
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
    builder.Services.ConfigureAll<Microsoft.AspNetCore.OpenApi.OpenApiOptions>(o =>
    {
        o.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        o.AddOperationTransformer<BearerSecuritySchemeTransformer>();
    });

    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy("El API está en ejecución."), tags: ["live"])
        .AddDbContextCheck<OpsynexDbContext>("database", tags: ["ready"])
        // Sin IA la plataforma funciona (fallback local): si no responde, el estado es Degraded.
        .AddCheck<Opsynex.Infrastructure.AiService.AiServiceHealthCheck>(
            "ai-service", failureStatus: HealthStatus.Degraded, tags: ["ready"]);

    // Autenticación JWT, políticas y tenant HTTP (antes de AddInfrastructure, que registra
    // un tenant sin filtro solo si no hay otro).
    builder.Services.AddOpsynexAuth(builder.Configuration);
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    // En Development se aplican las migraciones pendientes al arrancar (Database:MigrateOnStartup).
    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        await app.Services.InitializeDatabaseAsync();
    }

    if (runSeedCommand)
    {
        var result = await app.Services.SeedAdminAsync();
        if (result == SeedResult.NotConfigured)
        {
            Log.Error("Configura Seed:AdminEmail y Seed:AdminPassword (variables Seed__AdminEmail y Seed__AdminPassword)");
            Environment.ExitCode = 1;
        }
        else
        {
            Log.Information("seed-admin: {Result}", result);
        }

        return;
    }

    // Primer Admin desde configuración (no hace nada si no está configurado o ya existe).
    await app.Services.SeedAdminAsync();

    app.UseForwardedHeaders();
    app.UseCorrelationId();
    app.UseSecurityHeaders();
    if (!app.Environment.IsDevelopment())
    {
        // HSTS solo se envía en HTTPS (el balanceador termina TLS y avisa con X-Forwarded-Proto).
        app.UseHsts();
    }

    app.UseResponseCompression();
    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseSerilogRequestLogging(options =>
    {
        // Solo método, ruta, estado y duración: nunca el cuerpo ni la query (pueden traer datos).
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} respondió {StatusCode} en {Elapsed:0.0} ms";
        options.EnrichDiagnosticContext = (diagnostic, http) =>
            diagnostic.Set("UserId", http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? http.User.FindFirst("sub")?.Value);
    });

    app.UseCors(CorsExtensions.PolicyName);
    app.UseAuthentication();
    app.UseRateLimiter();

    // Valida pronto la empresa elegida por un Admin (X-Company-Id / companyId) para responder 400
    // si no es un Guid, antes de que la use una consulta.
    app.Use(async (context, next) =>
    {
        _ = context.RequestServices.GetRequiredService<ICurrentTenant>().IsFilterEnabled;
        await next(context);
    });

    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().WithDocumentPerVersion().AllowAnonymous();
        app.MapScalarApiReference(options => options.WithTitle("Opsynex AI API")).AllowAnonymous();
    }

    app.MapControllers();
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = HealthResponseWriter.WriteAsync,
    }).RequireCors(CorsExtensions.PolicyName).AllowAnonymous();

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
