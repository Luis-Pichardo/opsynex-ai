using System.Globalization;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Opsynex.Application.Common.Exceptions;

namespace Opsynex.Api.Extensions;

/// <summary>
/// Traduce las excepciones de la aplicación a ProblemDetails (RFC 7807) con un <c>code</c> estable.
/// Las demás excepciones las maneja el manejador por defecto (500, <c>internal_error</c>).
/// </summary>
public sealed class AppExceptionHandler(IProblemDetailsService problemDetails, TimeProvider timeProvider) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ProblemDetails? problem = exception switch
        {
            ValidationException validation => ValidationProblem(validation),
            BusinessValidationException business => new ValidationProblemDetails(
                new Dictionary<string, string[]> { [business.Field] = [business.Message] })
            {
                Status = StatusCodes.Status400BadRequest,
                Title = business.Title,
                Detail = business.Message,
            }.WithCode(business.Code),
            AuthenticationFailedException auth => Problem(StatusCodes.Status401Unauthorized, auth),
            AccountLockedException locked => Locked(httpContext, locked, timeProvider.GetUtcNow().UtcDateTime),
            ForbiddenException forbidden => Problem(StatusCodes.Status403Forbidden, forbidden),
            NotFoundException notFound => Problem(StatusCodes.Status404NotFound, notFound),
            ConflictException conflict => Problem(StatusCodes.Status409Conflict, conflict),
            PayloadTooLargeException tooLarge => Problem(StatusCodes.Status413PayloadTooLarge, tooLarge),
            UniqueConstraintViolationException unique => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Registro duplicado",
                Detail = unique.Message,
            }.WithCode("conflict"),
            ReferenceConstraintViolationException reference => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Registro en uso",
                Detail = reference.Message,
            }.WithCode("conflict.in_use"),
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, AppException exception) =>
        new ProblemDetails { Status = status, Title = exception.Title, Detail = exception.Message }.WithCode(exception.Code);

    private static ProblemDetails Locked(HttpContext httpContext, AccountLockedException exception, DateTime nowUtc)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling((exception.LockoutEndAt - nowUtc).TotalSeconds));
        httpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        var problem = Problem(StatusCodes.Status423Locked, exception);
        problem.Extensions["lockoutEndAt"] = exception.LockoutEndAt;
        return problem;
    }

    private static ValidationProblemDetails ValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => JsonNamingPolicyCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Datos no válidos",
            Detail = "Revisa los campos marcados.",
        }.WithCode("validation_failed");
    }

    private static string JsonNamingPolicyCamelCase(string name) =>
        System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(name);
}

internal static class ProblemDetailsCodeExtensions
{
    public static T WithCode<T>(this T problem, string code)
        where T : ProblemDetails
    {
        problem.Extensions[ProblemDetailsExtensions.CodeKey] = code;
        return problem;
    }
}
