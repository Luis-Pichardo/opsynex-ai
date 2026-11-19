using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Opsynex.Application.Abstractions.Security;
using Opsynex.Domain.Entities;

namespace Opsynex.Api.Auth;

/// <summary>Emite JWT firmados con HMAC-SHA256 con los claims sub, email, role y company_id.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var settings = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(settings.ExpireMinutes);

        var claims = new List<Claim>
        {
            new(OpsynexClaims.Subject, user.Id.ToString()),
            new(OpsynexClaims.Email, user.Email),
            new(OpsynexClaims.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        if (user.CompanyId is { } companyId)
        {
            claims.Add(new Claim(OpsynexClaims.CompanyId, companyId.ToString()));
        }

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(settings.SecretBytes), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expires);
    }
}
