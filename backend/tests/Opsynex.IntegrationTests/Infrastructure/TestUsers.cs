using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Opsynex.Application.Auth;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;
using Opsynex.Infrastructure.Security;

namespace Opsynex.IntegrationTests.Infrastructure;

/// <summary>Usuario de prueba creado directamente en la BD.</summary>
public sealed record TestUser(Guid Id, string Email, string Password, UserRole Role, Guid? CompanyId);

/// <summary>Crea empresas y usuarios de prueba y obtiene clientes HTTP autenticados.</summary>
public static class TestUsers
{
    public const string DefaultPassword = "Prueba12345";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<Guid> CreateCompanyAsync(DatabaseFixture db, string? name = null)
    {
        await using var context = db.CreateContext();
        var company = new Company { Name = name ?? $"Empresa {Guid.NewGuid():N}", Industry = Industry.Manufacturing };
        context.Companies.Add(company);
        await context.SaveChangesAsync(Ct);
        return company.Id;
    }

    public static async Task<TestUser> CreateUserAsync(
        DatabaseFixture db,
        UserRole role,
        Guid? companyId,
        string password = DefaultPassword,
        bool isActive = true)
    {
        await using var context = db.CreateContext();
        var user = new User
        {
            Email = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@opsynex.test",
            FullName = $"{role} de prueba",
            Role = role,
            CompanyId = companyId,
            PasswordHash = string.Empty,
            IsActive = isActive,
        };
        user.PasswordHash = new PasswordService().Hash(user, password);
        context.Users.Add(user);
        await context.SaveChangesAsync(Ct);
        return new TestUser(user.Id, user.Email, password, role, companyId);
    }

    /// <summary>Crea un usuario del rol indicado (con empresa nueva si no es Admin) y un cliente con su token.</summary>
    public static async Task<(HttpClient Client, TestUser User)> CreateAuthenticatedClientAsync(
        OpsynexApiFactory factory,
        UserRole role,
        Guid? companyId = null)
    {
        companyId ??= role == UserRole.Admin ? null : await CreateCompanyAsync(factory.Database);
        var user = await CreateUserAsync(factory.Database, role, companyId);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, user.Email, user.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, user);
    }

    /// <summary>Token firmado por el API para un usuario de prueba (sin pasar por el login).</summary>
    public static string IssueToken(OpsynexApiFactory factory, TestUser user)
    {
        using var scope = factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<Opsynex.Application.Abstractions.Security.ITokenService>();
        var entity = new Opsynex.Domain.Entities.User
        {
            Email = user.Email,
            FullName = "Usuario de prueba",
            PasswordHash = "x",
            Role = user.Role,
            CompanyId = user.CompanyId,
        };
        entity.Id = user.Id;
        return tokens.CreateAccessToken(entity).Token;
    }

    public static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, Ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Json.Options, Ct);
        return body!.AccessToken;
    }
}
