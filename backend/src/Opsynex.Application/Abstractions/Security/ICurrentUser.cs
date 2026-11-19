using Opsynex.Domain.Enums;

namespace Opsynex.Application.Abstractions.Security;

/// <summary>Usuario autenticado de la petición actual (a partir de los claims del JWT).</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? Email { get; }

    UserRole? Role { get; }

    /// <summary>Empresa a la que pertenece el usuario (null para un Admin global).</summary>
    Guid? CompanyId { get; }

    bool IsAdmin => Role == UserRole.Admin;
}
