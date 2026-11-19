using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Abstractions.Security;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Api.Auth;

/// <summary>Opciones de la sección <c>Seed</c> (primer Admin).</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    public string AdminFullName { get; set; } = "Administrador";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AdminEmail) && !string.IsNullOrWhiteSpace(AdminPassword);
}

/// <summary>Resultado del seed del Admin.</summary>
public enum SeedResult
{
    NotConfigured,
    Created,
    AlreadyExists,
}

/// <summary>
/// Crea el primer Admin (global, sin empresa) desde <c>Seed:AdminEmail</c> y <c>Seed:AdminPassword</c>.
/// Es idempotente: si el correo ya existe no modifica nada (no cambia la contraseña).
/// </summary>
public static partial class AdminSeeder
{
    public static async Task<SeedResult> SeedAdminAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var options = provider.GetRequiredService<IConfiguration>().GetSection(SeedOptions.SectionName).Get<SeedOptions>() ?? new SeedOptions();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AdminSeeder));

        if (!options.IsConfigured)
        {
            LogNotConfigured(logger);
            return SeedResult.NotConfigured;
        }

        var password = options.AdminPassword!;
        if (password.Length < Application.Common.PasswordRules.MinLength || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            throw new InvalidOperationException(
                $"Seed:AdminPassword no cumple la política: mínimo {Application.Common.PasswordRules.MinLength} caracteres con letras y números.");
        }

        var users = provider.GetRequiredService<IUserRepository>();
        var existing = await users.GetByEmailAsync(options.AdminEmail!, cancellationToken);
        if (existing is not null)
        {
            var maskedExisting = Application.Abstractions.Diagnostics.LogMask.Email(existing.Email);
            LogAlreadyExists(logger, maskedExisting, existing.Role);
            return SeedResult.AlreadyExists;
        }

        var admin = new User
        {
            Email = options.AdminEmail!.Trim(),
            FullName = options.AdminFullName,
            Role = UserRole.Admin,
            PasswordHash = string.Empty,
        };
        admin.PasswordHash = provider.GetRequiredService<IPasswordService>().Hash(admin, password);
        await users.AddAsync(admin, cancellationToken);
        await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

        var masked = Application.Abstractions.Diagnostics.LogMask.Email(admin.Email);
        LogCreated(logger, masked);
        return SeedResult.Created;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Seed del Admin omitido: Seed:AdminEmail y Seed:AdminPassword no están configurados")]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "El usuario {Email} ya existe (rol {Role}); el seed no lo modifica")]
    private static partial void LogAlreadyExists(ILogger logger, string email, UserRole role);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin inicial {Email} creado")]
    private static partial void LogCreated(ILogger logger, string email);
}
