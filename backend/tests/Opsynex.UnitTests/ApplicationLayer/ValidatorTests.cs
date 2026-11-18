using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Opsynex.Application;
using Opsynex.Application.Companies;
using Opsynex.Application.Users;
using Opsynex.Domain.Enums;

namespace Opsynex.UnitTests.ApplicationLayer;

public sealed class ValidatorTests
{
    private static readonly IServiceProvider Services = new ServiceCollection().AddApplication().BuildServiceProvider();

    private static IValidator<T> Validator<T>() => Services.GetRequiredService<IValidator<T>>();

    [Theory]
    [InlineData("Segura2026", true)]
    [InlineData("ñandú2026", true)]
    [InlineData("Corta1", false)]
    [InlineData("sololetras", false)]
    [InlineData("12345678", false)]
    [InlineData("", false)]
    public void Password_RequiresEightCharsWithLetterAndDigit(string password, bool valid)
    {
        var result = Validator<CreateUserRequest>().Validate(
            new CreateUserRequest("a@b.test", password, "Ana", UserRole.Admin, null));

        result.IsValid.ShouldBe(valid);
    }

    [Theory]
    [InlineData(UserRole.Analyst, false)]
    [InlineData(UserRole.Client, false)]
    [InlineData(UserRole.Admin, true)]
    public void CompanyId_IsRequiredExceptForAdmin(UserRole role, bool valid)
    {
        var result = Validator<CreateUserRequest>().Validate(
            new CreateUserRequest("a@b.test", "Segura2026", "Ana", role, null));

        result.IsValid.ShouldBe(valid);
    }

    [Theory]
    [InlineData("no-es-correo")]
    [InlineData("")]
    public void Email_MustBeValid(string email)
    {
        var result = Validator<UpdateUserRequest>().Validate(
            new UpdateUserRequest(email, "Ana", UserRole.Client, Guid.NewGuid(), true));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserRequest.Email));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("DOP", true)]
    [InlineData("usd", false)]
    [InlineData("EURO", false)]
    [InlineData("XYZ", false)]
    public void Company_CurrencyMustBeIsoCode(string? currency, bool valid)
    {
        Validator<CompanyRequest>().Validate(new CompanyRequest("Empresa", Industry.Services, null, currency))
            .IsValid.ShouldBe(valid);
    }
}
