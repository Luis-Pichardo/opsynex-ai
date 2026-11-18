using FluentValidation;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Companies;

public sealed record CompanyDto(
    Guid Id,
    string Name,
    Industry Industry,
    string? Sector,
    string Currency,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static CompanyDto From(Company company) => new(
        company.Id,
        company.Name,
        company.Industry,
        company.Sector,
        company.Currency,
        company.CreatedAt,
        company.UpdatedAt);
}

/// <summary>Alta o modificación de una empresa. <c>Currency</c> por defecto: USD.</summary>
public sealed record CompanyRequest(string Name, Industry Industry, string? Sector, string? Currency);

internal sealed class CompanyRequestValidator : AbstractValidator<CompanyRequest>
{
    public CompanyRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(200);
        RuleFor(r => r.Industry).IsInEnum().WithMessage("La industria no es válida.");
        RuleFor(r => r.Sector).MaximumLength(100);
        RuleFor(r => r.Currency)
            .Matches("^[A-Z]{3}$").WithMessage("La moneda debe ser un código ISO 4217 de 3 letras en mayúsculas (p. ej. USD, DOP).")
            .When(r => r.Currency is not null);
    }
}
