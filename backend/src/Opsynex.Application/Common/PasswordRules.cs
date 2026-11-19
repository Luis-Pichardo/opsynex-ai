using FluentValidation;

namespace Opsynex.Application.Common;

/// <summary>Política de contraseñas: mínimo 8 caracteres, con al menos una letra y un número.</summary>
public static class PasswordRules
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(MinLength).WithMessage($"La contraseña debe tener al menos {MinLength} caracteres.")
            .MaximumLength(MaxLength).WithMessage($"La contraseña no puede superar {MaxLength} caracteres.")
            .Must(p => p.Any(char.IsLetter)).WithMessage("La contraseña debe incluir al menos una letra.")
            .Must(p => p.Any(char.IsDigit)).WithMessage("La contraseña debe incluir al menos un número.");
}
