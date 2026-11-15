using Opsynex.Domain.Common;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Entities;

/// <summary>Usuario de la plataforma.</summary>
public sealed class User : Entity
{
    /// <summary>Correo (único, se guarda en minúsculas).</summary>
    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public required string FullName { get; set; }

    public UserRole Role { get; set; }

    /// <summary>Empresa del usuario; <c>null</c> para un administrador global.</summary>
    public Guid? CompanyId { get; set; }

    public bool IsActive { get; set; } = true;
}
