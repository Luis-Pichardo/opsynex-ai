using System.Text;

namespace Opsynex.Api.Auth;

/// <summary>Opciones de la sección <c>Jwt</c>.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Secreto HMAC-SHA256 (mínimo 32 bytes). Nunca se sube al repositorio.</summary>
    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "opsynex-api";

    public string Audience { get; set; } = "opsynex-web";

    public int ExpireMinutes { get; set; } = 60;

    internal byte[] SecretBytes => Encoding.UTF8.GetBytes(Secret);

    internal bool IsValid(out string error)
    {
        if (Encoding.UTF8.GetByteCount(Secret) < 32)
        {
            error = "Jwt:Secret debe tener al menos 32 bytes (variable Jwt__Secret).";
            return false;
        }

        if (ExpireMinutes is < 1 or > 24 * 60)
        {
            error = "Jwt:ExpireMinutes debe estar entre 1 y 1440.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
