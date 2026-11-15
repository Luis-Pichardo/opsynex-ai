namespace Opsynex.Application.Abstractions.Tenancy;

/// <summary>
/// Empresa del contexto actual. Lo usa el filtro global de EF Core: los usuarios no Admin
/// solo ven los datos de su empresa. Se conecta al usuario autenticado en la etapa 02.
/// </summary>
public interface ICurrentTenant
{
    /// <summary>Empresa del usuario actual (null si no hay usuario o es un admin global).</summary>
    Guid? CompanyId { get; }

    /// <summary><c>true</c> si las consultas deben limitarse a <see cref="CompanyId"/>.</summary>
    bool IsFilterEnabled { get; }
}
