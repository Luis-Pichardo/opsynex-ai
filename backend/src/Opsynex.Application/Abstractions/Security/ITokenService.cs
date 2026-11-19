using Opsynex.Domain.Entities;

namespace Opsynex.Application.Abstractions.Security;

/// <summary>Token de acceso emitido.</summary>
public sealed record AccessToken(string Token, DateTime ExpiresAt);

/// <summary>Emite tokens JWT.</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
}
