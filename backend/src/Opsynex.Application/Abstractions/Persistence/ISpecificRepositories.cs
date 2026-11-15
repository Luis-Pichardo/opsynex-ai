using Opsynex.Domain.Entities;

namespace Opsynex.Application.Abstractions.Persistence;

public interface IUserRepository : IRepository<User>
{
    /// <summary>Busca por correo sin distinguir mayúsculas. Ignora el filtro por empresa (login).</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}

/// <summary>Repositorio de datos anuales por empresa (FinancialData, OperationalData, KpiResult, Score).</summary>
public interface ICompanyYearRepository<TEntity> : IRepository<TEntity>
    where TEntity : Domain.Common.Entity, Domain.Common.ICompanyYearScoped
{
    Task<TEntity?> GetByCompanyAndYearAsync(Guid companyId, int year, CancellationToken cancellationToken = default);

    /// <summary>Todos los años de una empresa, ordenados de forma ascendente.</summary>
    Task<IReadOnlyList<TEntity>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
}

public interface IAccountMappingRepository : IRepository<AccountMapping>
{
    Task<AccountMapping?> GetByClientAccountAsync(Guid companyId, string clientAccount, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountMapping>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
}
