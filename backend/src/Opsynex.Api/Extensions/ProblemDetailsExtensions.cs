using System.Diagnostics;

namespace Opsynex.Api.Extensions;

public static class ProblemDetailsExtensions
{
    public const string CodeKey = "code";

    /// <summary>
    /// Errores en formato ProblemDetails (RFC 7807) con un <c>code</c> propio, el <c>traceId</c> y el
    /// <c>correlationId</c> (el mismo de los logs y de la cabecera <c>X-Correlation-Id</c>).
    /// </summary>
    public static IServiceCollection AddOpsynexProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx =>
        {
            var extensions = ctx.ProblemDetails.Extensions;
            var code = ctx.ProblemDetails is HttpValidationProblemDetails
                ? "validation_failed"
                : CodeForStatus(ctx.ProblemDetails.Status ?? ctx.HttpContext.Response.StatusCode);
            extensions.TryAdd(CodeKey, code);
            extensions.TryAdd("traceId", Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier);
            if (ctx.HttpContext.Items[Application.Abstractions.Diagnostics.CorrelationContext.LogProperty] is string correlationId)
            {
                extensions.TryAdd("correlationId", correlationId);
            }
        });

        return services;
    }

    public static string CodeForStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "bad_request",
        StatusCodes.Status401Unauthorized => "unauthorized",
        StatusCodes.Status403Forbidden => "forbidden",
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
        StatusCodes.Status409Conflict => "conflict",
        StatusCodes.Status413PayloadTooLarge => "payload_too_large",
        StatusCodes.Status415UnsupportedMediaType => "unsupported_media_type",
        StatusCodes.Status423Locked => "locked",
        StatusCodes.Status422UnprocessableEntity => "validation_failed",
        StatusCodes.Status429TooManyRequests => "too_many_requests",
        StatusCodes.Status503ServiceUnavailable => "service_unavailable",
        >= 500 => "internal_error",
        _ => "error",
    };
}
