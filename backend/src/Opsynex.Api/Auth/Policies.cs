using Microsoft.AspNetCore.Authorization;
using Opsynex.Domain.Enums;

namespace Opsynex.Api.Auth;

/// <summary>
/// Políticas de autorización (matriz de permisos):
/// <list type="table">
/// <item><term>CanManage</term><description>Admin: gestionar empresas y usuarios.</description></item>
/// <item><term>CanAnalyze</term><description>Admin y Analyst: subir archivos, homologar y recalcular.</description></item>
/// <item><term>CanView</term><description>Todos: ver KPIs, problemas, score y dashboard.</description></item>
/// </list>
/// </summary>
public static class Policies
{
    public const string CanManage = nameof(CanManage);
    public const string CanAnalyze = nameof(CanAnalyze);
    public const string CanView = nameof(CanView);

    public static AuthorizationBuilder AddOpsynexPolicies(this AuthorizationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .AddPolicy(CanManage, p => p.RequireAuthenticatedUser().RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(CanAnalyze, p => p.RequireAuthenticatedUser().RequireRole(nameof(UserRole.Admin), nameof(UserRole.Analyst)))
            .AddPolicy(CanView, p => p.RequireAuthenticatedUser().RequireRole(
                nameof(UserRole.Admin), nameof(UserRole.Analyst), nameof(UserRole.Client)))
            // Todo endpoint exige sesión salvo los marcados con [AllowAnonymous].
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
    }
}
