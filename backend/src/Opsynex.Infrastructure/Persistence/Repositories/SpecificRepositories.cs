using Microsoft.EntityFrameworkCore;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Domain.Common;
using Opsynex.Domain.Entities;

namespace Opsynex.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(OpsynexDbContext context) : Repository<User>(context), IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
#pragma warning disable CA1308 // Los correos se guardan en minúsculas.
        var normalized = email.Trim().ToLowerInvariant();
#pragma warning restore CA1308
        // El login ocurre antes de conocer la empresa: se ignora el filtro por empresa.
        return Set.IgnoreQueryFilters([OpsynexDbContext.CompanyFilter])
            .FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
    }

    public Task<User?> GetByIdIgnoringFiltersAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.IgnoreQueryFilters([OpsynexDbContext.CompanyFilter]).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesIgnoringFiltersAsync(
        IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var wanted = ids.Distinct().ToList();
        return await Set.IgnoreQueryFilters([OpsynexDbContext.CompanyFilter])
            .Where(u => wanted.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
    }
}

internal sealed class CompanyRepository(OpsynexDbContext context) : Repository<Company>(context), ICompanyRepository
{
    public Task<string?> FindNameIgnoringFiltersAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        Set.IgnoreQueryFilters([OpsynexDbContext.CompanyFilter])
            .Where(c => c.Id == companyId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);
}

internal sealed class CompanyYearRepository<TEntity>(OpsynexDbContext context)
    : Repository<TEntity>(context), ICompanyYearRepository<TEntity>
    where TEntity : Entity, ICompanyYearScoped
{
    public Task<TEntity?> GetByCompanyAndYearAsync(Guid companyId, int year, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(e => e.CompanyId == companyId && e.Year == year, cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await Set.Where(e => e.CompanyId == companyId).OrderBy(e => e.Year).ToListAsync(cancellationToken);
}

internal sealed class AccountMappingRepository(OpsynexDbContext context)
    : Repository<AccountMapping>(context), IAccountMappingRepository
{
    public Task<AccountMapping?> GetByClientAccountAsync(
        Guid companyId,
        string clientAccount,
        CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(m => m.CompanyId == companyId && m.ClientAccount == clientAccount, cancellationToken);

    public async Task<IReadOnlyList<AccountMapping>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await Set.Where(m => m.CompanyId == companyId).OrderBy(m => m.ClientAccount).ToListAsync(cancellationToken);
}
