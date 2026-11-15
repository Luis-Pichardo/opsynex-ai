namespace Opsynex.Domain.Common;

/// <summary>Base de todas las entidades persistidas.</summary>
public abstract class Entity
{
    /// <summary>Identificador (GUID v7, ordenable por fecha de creación).</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Fecha de creación en UTC (la asigna un interceptor de EF Core).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Fecha de la última modificación en UTC (la asigna un interceptor de EF Core).</summary>
    public DateTime UpdatedAt { get; set; }
}
