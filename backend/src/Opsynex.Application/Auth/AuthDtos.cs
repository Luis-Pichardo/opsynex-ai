using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

/// <summary>Respuesta de login. Nunca incluye la contraseña ni su hash.</summary>
public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAt, UserDto User);

/// <summary>Datos públicos de un usuario.</summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    UserRole Role,
    Guid? CompanyId,
    string? CompanyName,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt)
{
    public static UserDto From(User user, string? companyName) => new(
        user.Id,
        user.Email,
        user.FullName,
        user.Role,
        user.CompanyId,
        companyName,
        user.IsActive,
        user.LastLoginAt,
        user.CreatedAt);
}
