using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Opsynex.Api.Extensions;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Abstractions.Security;
using Opsynex.Application.Abstractions.Tenancy;

namespace Opsynex.Api.Auth;

public static class AuthenticationSetup
{
    private const string FailureCodeKey = "opsynex.auth.failure_code";

    /// <summary>JWT Bearer, políticas, usuario y tenant actuales y emisión de tokens.</summary>
    public static IServiceCollection AddOpsynexAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.IsValid(out _), "Configuración JWT inválida: Jwt:Secret debe tener al menos 32 bytes y Jwt:ExpireMinutes estar entre 1 y 1440.")
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<ICurrentTenant, HttpCurrentTenant>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((bearer, jwt, timeProvider) =>
            {
                var settings = jwt.Value;
                bearer.MapInboundClaims = false;
                bearer.TimeProvider = timeProvider;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(settings.SecretBytes),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    // Usa el TimeProvider de la aplicación (en pruebas se puede adelantar el reloj).
                    LifetimeValidator = (notBefore, expires, _, parameters) =>
                        ValidateLifetime(notBefore, expires, parameters, timeProvider.GetUtcNow().UtcDateTime),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = OpsynexClaims.Email,
                    RoleClaimType = OpsynexClaims.Role,
                };
                bearer.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateSessionAsync,
                    OnChallenge = WriteChallengeAsync,
                    OnForbidden = WriteForbiddenAsync,
                };
            });

        services.AddAuthorizationBuilder().AddOpsynexPolicies();
        return services;
    }

    /// <summary>
    /// El token sigue siendo válido solo si el usuario existe, está activo y su rol y empresa
    /// no cambiaron desde que se emitió (si cambian, debe volver a iniciar sesión).
    /// </summary>
    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (!Guid.TryParse(principal?.FindFirst(OpsynexClaims.Subject)?.Value, out var userId))
        {
            Fail(context, "auth.invalid_token");
            return;
        }

        var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdIgnoringFiltersAsync(userId, context.HttpContext.RequestAborted);
        var tokenCompany = principal!.FindFirst(OpsynexClaims.CompanyId)?.Value;
        if (user is null
            || !user.IsActive
            || user.Role.ToString() != principal.FindFirst(OpsynexClaims.Role)?.Value
            || user.CompanyId?.ToString() != tokenCompany)
        {
            Fail(context, "auth.session_invalid");
        }
    }

    private static bool ValidateLifetime(DateTime? notBefore, DateTime? expires, TokenValidationParameters parameters, DateTime nowUtc)
    {
        if (notBefore is { } nbf && nbf > nowUtc.Add(parameters.ClockSkew))
        {
            throw new SecurityTokenNotYetValidException("El token todavía no es válido.") { NotBefore = nbf };
        }

        if (expires is not { } exp)
        {
            throw new SecurityTokenNoExpirationException("El token no tiene fecha de expiración.");
        }

        if (exp < nowUtc.Subtract(parameters.ClockSkew))
        {
            throw new SecurityTokenExpiredException("El token expiró.") { Expires = exp };
        }

        return true;
    }

    private static void Fail(TokenValidatedContext context, string code)
    {
        context.HttpContext.Items[FailureCodeKey] = code;
        context.Fail("La sesión ya no es válida.");
    }

    private static async Task WriteChallengeAsync(JwtBearerChallengeContext context)
    {
        context.HandleResponse();

        var (code, detail) = context.AuthenticateFailure switch
        {
            SecurityTokenExpiredException => ("auth.token_expired", "La sesión expiró. Inicia sesión de nuevo."),
            not null when context.HttpContext.Items[FailureCodeKey] is string stored =>
                (stored, "La sesión ya no es válida. Inicia sesión de nuevo."),
            not null => ("auth.invalid_token", "El token de acceso no es válido."),
            null => ("auth.unauthorized", "Inicia sesión para continuar."),
        };

        context.Response.Headers.WWWAuthenticate = "Bearer";
        await WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "No autenticado", detail, code);
    }

    private static Task WriteForbiddenAsync(ForbiddenContext context) =>
        WriteProblemAsync(
            context.HttpContext,
            StatusCodes.Status403Forbidden,
            "Acceso denegado",
            "Tu rol no tiene permiso para esta acción.",
            "auth.forbidden");

    private static async Task WriteProblemAsync(HttpContext http, int status, string title, string detail, string code)
    {
        http.Response.StatusCode = status;
        var problemDetails = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions[ProblemDetailsExtensions.CodeKey] = code;
        await problemDetails.WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem });
    }
}
