using Microsoft.AspNetCore.Identity;
using Opsynex.Application.Abstractions.Security;
using Opsynex.Domain.Entities;

namespace Opsynex.Infrastructure.Security;

/// <summary>
/// Hash de contraseñas con <see cref="PasswordHasher{TUser}"/> de ASP.NET Core Identity
/// (PBKDF2 con HMAC-SHA512, sal aleatoria y 100 000 iteraciones). La contraseña nunca se guarda.
/// </summary>
internal sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public PasswordVerification Verify(User user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
        {
            return PasswordVerification.Failed;
        }

        return _hasher.VerifyHashedPassword(user, hashedPassword, providedPassword) switch
        {
            PasswordVerificationResult.Success => PasswordVerification.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessRehashNeeded,
            _ => PasswordVerification.Failed,
        };
    }
}
