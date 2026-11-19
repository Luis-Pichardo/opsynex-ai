using FluentValidation;
using Microsoft.Extensions.Options;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Abstractions.Security;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Entities;

namespace Opsynex.Application.Auth;

/// <summary>Login y sesión. El alta de usuarios la hace <see cref="Users.UserService"/> (solo Admin).</summary>
public sealed class AuthService(
    IUserRepository users,
    ICompanyRepository companies,
    IUnitOfWork unitOfWork,
    IPasswordService passwords,
    ITokenService tokens,
    ICurrentUser currentUser,
    IValidator<LoginRequest> loginValidator,
    IOptions<AuthOptions> options,
    TimeProvider timeProvider)
{
    // Hash ficticio para que un correo inexistente tarde lo mismo que una contraseña errónea.
    private static readonly User DummyUser = new() { Email = "dummy@opsynex.invalid", PasswordHash = string.Empty, FullName = "dummy" };
    private static string? _dummyHash;

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = await users.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null)
        {
            _dummyHash ??= passwords.Hash(DummyUser, "dummy-password-1");
            passwords.Verify(DummyUser, _dummyHash, request.Password);
            throw AuthenticationFailedException.InvalidCredentials();
        }

        if (user.IsLockedOut(now))
        {
            throw new AccountLockedException(user.LockoutEndAt!.Value);
        }

        var verification = passwords.Verify(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerification.Failed)
        {
            var settings = options.Value;
            var locked = user.RegisterFailedLogin(now, settings.MaxFailedAttempts, TimeSpan.FromMinutes(settings.LockoutMinutes));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            if (locked)
            {
                throw new AccountLockedException(user.LockoutEndAt!.Value);
            }

            throw AuthenticationFailedException.InvalidCredentials();
        }

        if (!user.IsActive)
        {
            throw new ForbiddenException("auth.user_inactive", "El usuario está desactivado. Contacta al administrador.");
        }

        if (verification == PasswordVerification.SuccessRehashNeeded)
        {
            user.PasswordHash = passwords.Hash(user, request.Password);
        }

        user.RegisterSuccessfulLogin(now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var token = tokens.CreateAccessToken(user);
        var companyName = user.CompanyId is { } companyId
            ? await companies.FindNameIgnoringFiltersAsync(companyId, cancellationToken)
            : null;

        return new LoginResponse(token.Token, "Bearer", token.ExpiresAt, UserDto.From(user, companyName));
    }

    /// <summary>Datos del usuario autenticado.</summary>
    public async Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException("auth.unauthorized", "Inicia sesión para continuar.");

        var user = await users.GetByIdIgnoringFiltersAsync(userId, cancellationToken)
            ?? throw new AuthenticationFailedException("auth.unauthorized", "La sesión ya no es válida.");

        var companyName = user.CompanyId is { } companyId
            ? await companies.FindNameIgnoringFiltersAsync(companyId, cancellationToken)
            : null;
        return UserDto.From(user, companyName);
    }
}
