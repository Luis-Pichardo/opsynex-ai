using Opsynex.Application.Abstractions.Tenancy;

namespace Opsynex.Infrastructure.Tenancy;

/// <summary>
/// Tenant sin filtro (procesos internos, migraciones). En la etapa 02 se reemplaza en el API
/// por un tenant que lee la empresa del usuario autenticado.
/// </summary>
public sealed class UnrestrictedTenant : ICurrentTenant
{
    public Guid? CompanyId => null;

    public bool IsFilterEnabled => false;
}

/// <summary>Tenant fijo de una empresa (útil en procesos en segundo plano y pruebas).</summary>
public sealed class FixedTenant(Guid companyId) : ICurrentTenant
{
    public Guid? CompanyId { get; } = companyId;

    public bool IsFilterEnabled => true;
}
