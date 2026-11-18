namespace Opsynex.Application.Auth;

/// <summary>Opciones de la sección <c>Auth</c>.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Intentos fallidos consecutivos antes de bloquear el login.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>Duración del bloqueo temporal, en minutos.</summary>
    public int LockoutMinutes { get; set; } = 15;
}
