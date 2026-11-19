namespace Opsynex.Application.Common.Exceptions;

/// <summary>
/// Error de negocio con un <see cref="Code"/> propio. El API lo traduce a ProblemDetails
/// (RFC 7807) con el estado HTTP que corresponde a cada subclase.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Código estable para el frontend (p. ej. <c>auth.invalid_credentials</c>).</summary>
    public string Code { get; }

    /// <summary>Título corto para mostrar.</summary>
    public abstract string Title { get; }
}

/// <summary>401: credenciales inválidas o sesión no válida.</summary>
public sealed class AuthenticationFailedException(string code, string message) : AppException(code, message)
{
    public override string Title => "No autenticado";

    public static AuthenticationFailedException InvalidCredentials() =>
        new("auth.invalid_credentials", "El correo o la contraseña no son correctos.");
}

/// <summary>423: cuenta bloqueada temporalmente por intentos fallidos.</summary>
public sealed class AccountLockedException(DateTime lockoutEndAt)
    : AppException("auth.locked_out", "La cuenta está bloqueada temporalmente por varios intentos fallidos. Inténtalo más tarde.")
{
    public DateTime LockoutEndAt { get; } = lockoutEndAt;

    public override string Title => "Cuenta bloqueada";
}

/// <summary>403: el usuario no tiene permiso para la acción.</summary>
public sealed class ForbiddenException(string code, string message) : AppException(code, message)
{
    public override string Title => "Acceso denegado";
}

/// <summary>404: el recurso no existe o pertenece a otra empresa.</summary>
public sealed class NotFoundException(string resource, object key)
    : AppException("not_found", $"No se encontró {resource} «{key}».")
{
    public override string Title => "No encontrado";
}

/// <summary>409: el estado actual impide la operación.</summary>
public sealed class ConflictException(string code, string message, Exception? innerException = null)
    : AppException(code, message, innerException)
{
    public override string Title => "Conflicto";
}

/// <summary>400: regla de negocio no cumplida sobre un campo concreto.</summary>
public sealed class BusinessValidationException(string code, string field, string message) : AppException(code, message)
{
    public string Field { get; } = field;

    public override string Title => "Datos no válidos";
}
