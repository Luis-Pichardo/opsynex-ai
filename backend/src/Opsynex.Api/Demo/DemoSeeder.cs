using System.Diagnostics;
using System.Security.Claims;
using Opsynex.Api.Auth;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Abstractions.Security;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Api.Demo;

/// <summary>Sección <c>Demo</c>: datos de demostración (comando <c>seed-demo</c>).</summary>
public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    /// <summary>Carpeta con los libros sintéticos (por defecto, <c>sample-data</c> junto al API).</summary>
    public string? SampleDataPath { get; set; }

    /// <summary>Contraseña de todos los usuarios demo (por defecto, <see cref="DemoSeeder.DefaultPassword"/>).</summary>
    public string? Password { get; set; }
}

/// <summary>Usuario demo creado (para el resumen del comando).</summary>
public sealed record DemoUser(string Email, UserRole Role, string? Company);

/// <summary>Resultado de <c>seed-demo</c>.</summary>
public sealed record DemoSeedResult(IReadOnlyList<DemoUser> Users, IReadOnlyList<string> Steps, TimeSpan Elapsed);

/// <summary>
/// Entorno de demostración (etapa 13): dos empresas ficticias con datos <b>sintéticos</b>, usuarios
/// de cada rol y el análisis ya ejecutado. Las cargas pasan por el mismo flujo que la interfaz
/// (como el Analyst de cada empresa), así que el resultado es el mismo que al subir los archivos.
/// Es idempotente: si una empresa demo ya existe, no vuelve a cargar sus datos.
/// </summary>
public static class DemoSeeder
{
    public const string Command = "seed-demo";
    public const string DefaultPassword = "Demo2026!";

    private sealed record DemoCompany(string Name, Industry Industry, string Sector, string Slug, string[] Files);

    private static readonly DemoCompany[] Companies =
    [
        new("Manufacturas Demo del Caribe, S.R.L.", Industry.Manufacturing, "Plásticos y empaques", "manufactura",
        [
            "libro_sintetico_3a.xls",
            "industrial/estandares.xlsx",
            "industrial/costos.xlsx",
            "industrial/produccion.xlsx",
        ]),
        new("Servicios Demo Consultores, S.R.L.", Industry.Services, "Consultoría y servicios técnicos", "servicios",
        [
            "libro_sintetico_2a.xls",
        ]),
    ];

    public static async Task<DemoSeedResult> SeedDemoAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var configuration = services.GetRequiredService<IConfiguration>();
        var options = configuration.GetSection(DemoOptions.SectionName).Get<DemoOptions>() ?? new DemoOptions();
        var password = string.IsNullOrWhiteSpace(options.Password) ? DefaultPassword : options.Password;
        var sampleData = options.SampleDataPath ?? Path.Combine(AppContext.BaseDirectory, "sample-data");
        var steps = new List<string>();
        var users = new List<DemoUser>();

        await using (var scope = services.CreateAsyncScope())
        {
            var admin = await EnsureUserAsync(scope.ServiceProvider, "admin@demo.opsynex.local", "Administración Demo", UserRole.Admin, null, password, cancellationToken);
            users.Add(new DemoUser(admin.Email, admin.Role, null));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
        }

        foreach (var demo in Companies)
        {
            Guid companyId;
            bool created;
            Guid analystId;
            await using (var scope = services.CreateAsyncScope())
            {
                var provider = scope.ServiceProvider;
                var companies = provider.GetRequiredService<ICompanyRepository>();
                var company = await companies.FirstOrDefaultAsync(c => c.Name == demo.Name, cancellationToken);
                created = company is null;
                if (company is null)
                {
                    company = new Company { Name = demo.Name, Industry = demo.Industry, Sector = demo.Sector, Currency = "DOP" };
                    await companies.AddAsync(company, cancellationToken);
                }

                companyId = company.Id;
                var analyst = await EnsureUserAsync(provider, $"analista.{demo.Slug}@demo.opsynex.local", $"Analista {demo.Slug}", UserRole.Analyst, companyId, password, cancellationToken);
                var client = await EnsureUserAsync(provider, $"cliente.{demo.Slug}@demo.opsynex.local", $"Cliente {demo.Slug}", UserRole.Client, companyId, password, cancellationToken);
                analystId = analyst.Id;
                users.Add(new DemoUser(analyst.Email, analyst.Role, demo.Name));
                users.Add(new DemoUser(client.Email, client.Role, demo.Name));
                await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
            }

            if (!created)
            {
                steps.Add($"{demo.Name}: ya existía; no se volvieron a cargar sus datos.");
                continue;
            }

            foreach (var file in demo.Files)
            {
                var path = Path.Combine(sampleData, file);
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"No se encontró el archivo de demostración «{path}». Configura Demo:SampleDataPath.", path);
                }

                // Cada archivo se sube como lo haría el Analyst de la empresa desde la interfaz.
                await using var scope = services.CreateAsyncScope();
                ActAs(scope.ServiceProvider, analystId, UserRole.Analyst, companyId);
                await using var stream = File.OpenRead(path);
                var report = await scope.ServiceProvider.GetRequiredService<UploadService>().UploadAsync(
                    new UploadFileRequest(stream, Path.GetFileName(file), stream.Length, companyId, null), cancellationToken);
                steps.Add($"{demo.Name}: {Path.GetFileName(file)} → {report.Status} ({report.AnalysisStatus}, {report.RowsCount} registros).");
            }
        }

        return new DemoSeedResult(users, steps, watch.Elapsed);
    }

    /// <summary>Simula la sesión de un usuario en el scope (sin HTTP) para reutilizar los servicios.</summary>
    private static void ActAs(IServiceProvider provider, Guid userId, UserRole role, Guid companyId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(OpsynexClaims.Subject, userId.ToString()),
                new Claim(OpsynexClaims.Role, role.ToString()),
                new Claim(OpsynexClaims.CompanyId, companyId.ToString()),
            ],
            authenticationType: "seed-demo");
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            RequestServices = provider,
        };
    }

    private static async Task<User> EnsureUserAsync(
        IServiceProvider provider, string email, string fullName, UserRole role, Guid? companyId, string password, CancellationToken cancellationToken)
    {
        var users = provider.GetRequiredService<IUserRepository>();
        var user = await users.GetByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            user = new User { Email = email, FullName = fullName, Role = role, CompanyId = companyId, PasswordHash = string.Empty };
            await users.AddAsync(user, cancellationToken);
        }

        // La contraseña demo se restablece en cada ejecución (para presentar sin sorpresas).
        user.PasswordHash = provider.GetRequiredService<IPasswordService>().Hash(user, password);
        user.IsActive = true;
        user.ClearLockout();
        return user;
    }
}
