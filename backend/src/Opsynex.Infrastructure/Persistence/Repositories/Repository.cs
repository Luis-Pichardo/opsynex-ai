using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Domain.Common;

namespace Opsynex.Infrastructure.Persistence.Repositories;

/// <summary>Repositorio genérico sobre EF Core. Respeta el filtro global por empresa.</summary>
internal class Repository<TEntity>(OpsynexDbContext context) : IRepository<TEntity>
    where TEntity : Entity
{
    protected OpsynexDbContext Context { get; } = context;

    protected DbSet<TEntity> Set => Context.Set<TEntity>();

    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await Set.ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await Set.Where(predicate).ToListAsync(cancellationToken);

    public Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(predicate, cancellationToken);

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(predicate, cancellationToken);

    public Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default) =>
        predicate is null ? Set.CountAsync(cancellationToken) : Set.CountAsync(predicate, cancellationToken);

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        await Set.AddAsync(entity, cancellationToken);

    public Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default) =>
        Set.AddRangeAsync(entities, cancellationToken);

    public void Update(TEntity entity) => Set.Update(entity);

    public void Remove(TEntity entity) => Set.Remove(entity);

    public void RemoveRange(IEnumerable<TEntity> entities) => Set.RemoveRange(entities);
}
