using Opsynex.Application.Abstractions.Security;
using Opsynex.Application.Abstractions.Tenancy;
using Opsynex.Application.Common.Exceptions;

namespace Opsynex.Api.Auth;

/// <summary>
/// Empresa de la petición para el filtro global de EF Core:
/// <list type="bullet">
/// <item>Sin petición HTTP (procesos internos, seed): sin filtro.</item>
/// <item>Petición anónima: filtro activo sin empresa → no ve datos.</item>
/// <item>Analyst y Client: fijo a su empresa (se ignora cualquier selección).</item>
/// <item>Admin: ve todo, o solo la empresa indicada en la cabecera <c>X-Company-Id</c>
/// o en el parámetro <c>companyId</c>.</item>
/// </list>
/// </summary>
public sealed class HttpCurrentTenant(IHttpContextAccessor accessor, ICurrentUser currentUser) : ICurrentTenant
{
    public const string HeaderName = "X-Company-Id";
    public const string QueryName = "companyId";

    private (bool Enabled, Guid? CompanyId)? _resolved;

    public Guid? CompanyId => Resolve().CompanyId;

    public bool IsFilterEnabled => Resolve().Enabled;

    private (bool Enabled, Guid? CompanyId) Resolve() => _resolved ??= Compute();

    private (bool Enabled, Guid? CompanyId) Compute()
    {
        var http = accessor.HttpContext;
        if (http is null)
        {
            return (false, null);
        }

        if (!currentUser.IsAuthenticated)
        {
            return (true, null);
        }

        if (!currentUser.IsAdmin)
        {
            return (true, currentUser.CompanyId);
        }

        var selected = http.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(selected))
        {
            selected = http.Request.Query[QueryName].FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(selected))
        {
            return (false, null);
        }

        return Guid.TryParse(selected, out var companyId)
            ? (true, companyId)
            : throw new BusinessValidationException(
                "tenant.invalid_company_id",
                QueryName,
                $"El valor «{selected}» de {HeaderName}/{QueryName} no es un identificador de empresa válido.");
    }
}
