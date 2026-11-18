using Opsynex.Domain.Entities;

namespace Opsynex.Application.Abstractions.Security;

/// <summary>Resultado de verificar una contraseña.</summary>
public enum PasswordVerification
{
    Failed,
    Success,

    /// <summary>Correcta, pero el hash usa un algoritmo antiguo y debe regenerarse.</summary>
    SuccessRehashNeeded,
}

/// <summary>Hash seguro de contraseñas (implementado con <c>PasswordHasher&lt;User&gt;</c>).</summary>
public interface IPasswordService
{
    string Hash(User user, string password);

    PasswordVerification Verify(User user, string hashedPassword, string providedPassword);
}
