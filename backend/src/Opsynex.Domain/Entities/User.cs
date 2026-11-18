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

    /// <summary>Intentos de login fallidos consecutivos.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>Hasta cuándo está bloqueado el login (UTC); <c>null</c> si no está bloqueado.</summary>
    public DateTime? LockoutEndAt { get; set; }

    /// <summary>Último login correcto (UTC).</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Indica si el usuario está bloqueado en el instante indicado.</summary>
    public bool IsLockedOut(DateTime nowUtc) => LockoutEndAt is { } end && end > nowUtc;

    /// <summary>
    /// Registra un intento fallido; al llegar a <paramref name="maxAttempts"/> bloquea el login
    /// durante <paramref name="lockoutDuration"/> y reinicia el contador.
    /// </summary>
    /// <returns><c>true</c> si este intento provocó el bloqueo.</returns>
    public bool RegisterFailedLogin(DateTime nowUtc, int maxAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginCount++;
        if (FailedLoginCount < maxAttempts)
        {
            return false;
        }

        FailedLoginCount = 0;
        LockoutEndAt = nowUtc.Add(lockoutDuration);
        return true;
    }

    /// <summary>Registra un login correcto: reinicia el contador y quita el bloqueo.</summary>
    public void RegisterSuccessfulLogin(DateTime nowUtc)
    {
        FailedLoginCount = 0;
        LockoutEndAt = null;
        LastLoginAt = nowUtc;
    }

    /// <summary>Quita el bloqueo (p. ej. cuando un Admin restablece la contraseña).</summary>
    public void ClearLockout()
    {
        FailedLoginCount = 0;
        LockoutEndAt = null;
    }
}
