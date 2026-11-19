using FluentValidation;

namespace Opsynex.Application.Auth;

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().WithMessage("El correo es obligatorio.").MaximumLength(256);
        RuleFor(r => r.Password).NotEmpty().WithMessage("La contraseña es obligatoria.").MaximumLength(256);
    }
}
