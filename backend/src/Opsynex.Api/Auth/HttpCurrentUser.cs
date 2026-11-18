using Opsynex.Application.Abstractions.Security;
using Opsynex.Domain.Enums;

namespace Opsynex.Api.Auth;

/// <summary>Lee el usuario autenticado de los claims del JWT de la petición.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => ParseGuid(OpsynexClaims.Subject);

    public string? Email => IsAuthenticated ? Principal!.FindFirst(OpsynexClaims.Email)?.Value : null;

    public UserRole? Role =>
        IsAuthenticated && Enum.TryParse<UserRole>(Principal!.FindFirst(OpsynexClaims.Role)?.Value, out var role) ? role : null;

    public Guid? CompanyId => ParseGuid(OpsynexClaims.CompanyId);

    private Guid? ParseGuid(string claim) =>
        IsAuthenticated && Guid.TryParse(Principal!.FindFirst(claim)?.Value, out var value) ? value : null;
}
