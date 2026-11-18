namespace Opsynex.Application.Abstractions.Persistence;

/// <summary>Confirma los cambios pendientes de los repositorios en una sola transacción.</summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Guarda los cambios. Lanza <see cref="Common.Exceptions.UniqueConstraintViolationException"/>
    /// si se viola una restricción única (p. ej. dos registros para la misma empresa y año).
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
