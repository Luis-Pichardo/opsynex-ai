using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;
using Opsynex.Infrastructure.Persistence;

namespace Opsynex.IntegrationTests.Persistence;

/// <summary>Crea datos de prueba aislados (cada llamada usa una empresa nueva).</summary>
internal static class TestData
{
    public static Company NewCompany(string? name = null) => new()
    {
        Name = name ?? $"Empresa {Guid.NewGuid():N}",
        Industry = Industry.Manufacturing,
        Sector = "Alimentos",
    };

    public static User NewUser(Guid? companyId, string? email = null, UserRole role = UserRole.Analyst) => new()
    {
        Email = email ?? $"user-{Guid.NewGuid():N}@opsynex.test",
        PasswordHash = "hash",
        FullName = "Usuario de prueba",
        Role = role,
        CompanyId = companyId,
    };

    public static FinancialData NewFinancialData(Guid companyId, int year, decimal revenue = 1_000_000m) => new()
    {
        CompanyId = companyId,
        Year = year,
        Revenue = revenue,
        Cogs = revenue * 0.6m,
        Ebitda = revenue * 0.15m,
    };

    /// <summary>Guarda una empresa nueva y devuelve su Id.</summary>
    public static async Task<Guid> SeedCompanyAsync(OpsynexDbContext context, CancellationToken cancellationToken)
    {
        var company = NewCompany();
        context.Companies.Add(company);
        await context.SaveChangesAsync(cancellationToken);
        return company.Id;
    }
}
