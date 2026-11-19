namespace Opsynex.Application.Abstractions.Persistence;

/// <summary>
/// Indica si las consultas de la operación actual pueden hacerse sin seguimiento de cambios
/// (<c>AsNoTracking</c>): el API responde que sí en las peticiones de solo lectura (GET y HEAD).
/// Sin implementación registrada (seeds, trabajos en segundo plano) las consultas se siguen.
/// </summary>
public interface IQueryTrackingPolicy
{
    bool PreferNoTracking { get; }
}
