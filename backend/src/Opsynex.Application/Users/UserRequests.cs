using FluentValidation;
using Opsynex.Application.Common;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Users;

/// <summary>Campos comunes del alta y la modificación de usuarios.</summary>
public interface IUserFields
{
    string Email { get; }

    string FullName { get; }

    UserRole Role { get; }

    Guid? CompanyId { get; }
}

/// <summary>Alta de usuario (solo Admin). Un Admin puede no tener empresa; Analyst y Client sí.</summary>
public sealed record CreateUserRequest(string Email, string Password, string FullName, UserRole Role, Guid? CompanyId)
    : IUserFields;

public sealed record UpdateUserRequest(string Email, string FullName, UserRole Role, Guid? CompanyId, bool IsActive)
    : IUserFields;

public sealed record SetPasswordRequest(string NewPassword);

/// <summary>Filtros del listado de usuarios.</summary>
public sealed record UserQuery(Guid? CompanyId = null, UserRole? Role = null, string? Search = null);

internal abstract class UserFieldsValidator<T> : AbstractValidator<T>
    where T : IUserFields
{
    protected UserFieldsValidator()
    {
        RuleFor(r => r.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(256)
            .EmailAddress().WithMessage("El correo no es válido.");
        RuleFor(r => r.FullName)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(200);
        RuleFor(r => r.Role).IsInEnum().WithMessage("El rol no es válido.");
        RuleFor(r => r.CompanyId)
            .NotNull()
            .When(r => r.Role != UserRole.Admin)
            .WithMessage("Los usuarios Analyst y Client deben pertenecer a una empresa.");
    }
}

internal sealed class CreateUserRequestValidator : UserFieldsValidator<CreateUserRequest>
{
    public CreateUserRequestValidator() => RuleFor(r => r.Password).ValidPassword();
}

internal sealed class UpdateUserRequestValidator : UserFieldsValidator<UpdateUserRequest>;

internal sealed class SetPasswordRequestValidator : AbstractValidator<SetPasswordRequest>
{
    public SetPasswordRequestValidator() => RuleFor(r => r.NewPassword).ValidPassword();
}
