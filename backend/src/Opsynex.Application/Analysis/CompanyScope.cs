using Opsynex.Application.Abstractions.Security;
using Opsynex.Application.Abstractions.Tenancy;
using Opsynex.Application.Common.Exceptions;

namespace Opsynex.Application.Analysis;

/// <summary>Empresa sobre la que opera la petición: la del usuario, o la que eligió el Admin.</summary>
public sealed class CompanyScope(ICurrentUser currentUser, ICurrentTenant tenant)
{
    /// <summary>Empresa actual o error 400 con el código indicado si el Admin no eligió ninguna.</summary>
    public Guid Require(string errorCode) =>
        (currentUser.IsAdmin ? tenant.CompanyId : currentUser.CompanyId)
        ?? throw new BusinessValidationException(errorCode, "companyId", "Indica la empresa (cabecera X-Company-Id o parámetro companyId).");
}
