using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Opsynex.Application.Abstractions.Diagnostics;
using Serilog.Context;

namespace Opsynex.Api.Extensions;

/// <summary>Sección <c>RateLimiting</c>: límites por ventana fija de los endpoints sensibles.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    /// <summary>Intentos de login por IP y ventana (fuerza bruta; además del bloqueo por cuenta).</summary>
    public int LoginPermitLimit { get; set; } = 10;

    public int LoginWindowSeconds { get; set; } = 60;

    /// <summary>Cargas de archivos por usuario y ventana (cada carga ejecuta el análisis).</summary>
    public int UploadPermitLimit { get; set; } = 20;

    public int UploadWindowSeconds { get; set; } = 60;
}

/// <summary>
/// Endurecimiento del API (sección 19 de las especificaciones): limitación de peticiones,
/// cabeceras de seguridad, id de correlación, compresión y cabeceras de proxy.
/// </summary>
public static class SecurityExtensions
{
    public const string LoginPolicy = "login";
    public const string UploadPolicy = "uploads";

    public static IServiceCollection AddOpsynexSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .Validate(o => o.LoginPermitLimit > 0 && o.LoginWindowSeconds > 0 && o.UploadPermitLimit > 0 && o.UploadWindowSeconds > 0,
                "RateLimiting: los límites y ventanas deben ser mayores que 0.")
            .ValidateOnStart();
        var limits = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var http = context.HttpContext;
                var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? (int)Math.Ceiling(retryAfter.TotalSeconds)
                    : 60;
                http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Demasiadas solicitudes",
                    Detail = $"Hiciste demasiadas solicitudes seguidas. Espera {seconds} segundos y vuelve a intentarlo.",
                };
                problem.Extensions[ProblemDetailsExtensions.CodeKey] = "rate_limited";
                await http.RequestServices.GetRequiredService<IProblemDetailsService>()
                    .WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem });
            };

            options.AddPolicy(LoginPolicy, http => limits.Enabled
                ? RateLimitPartition.GetFixedWindowLimiter(
                    $"login:{http.Connection.RemoteIpAddress}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.LoginPermitLimit,
                        Window = TimeSpan.FromSeconds(limits.LoginWindowSeconds),
                        QueueLimit = 0,
                    })
                : RateLimitPartition.GetNoLimiter("login"));

            options.AddPolicy(UploadPolicy, http => limits.Enabled
                ? RateLimitPartition.GetFixedWindowLimiter(
                    $"upload:{http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub") ?? http.Connection.RemoteIpAddress?.ToString()}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.UploadPermitLimit,
                        Window = TimeSpan.FromSeconds(limits.UploadWindowSeconds),
                        QueueLimit = 0,
                    })
                : RateLimitPartition.GetNoLimiter("upload"));
        });

        // Compresión de respuestas JSON (las respuestas de KPIs y problemas son grandes). El PDF y
        // los .xlsx ya están comprimidos y no se tocan.
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ["application/json", "application/problem+json", "text/plain"];
        });

        // Detrás de un proxy o balanceador (Azure/AWS/GCP): IP real del cliente y esquema HTTPS.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = configuration.GetValue("ReverseProxy:ForwardLimit", 1);
            if (configuration.GetValue<bool>("ReverseProxy:TrustAll"))
            {
                // Solo si el API no es accesible directamente (red privada detrás del balanceador).
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            }
        });

        return services;
    }

    /// <summary>
    /// Id de correlación: usa el <c>X-Correlation-Id</c> recibido (si es válido) o crea uno; lo
    /// devuelve en la respuesta, lo agrega a todos los logs de la petición y lo propaga al servicio de IA.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var id = CorrelationContext.Sanitize(context.Request.Headers[CorrelationContext.HeaderName].ToString())
                ?? CorrelationContext.NewId();
            CorrelationContext.Current = id;
            context.Items[CorrelationContext.LogProperty] = id;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationContext.HeaderName] = id;
                return Task.CompletedTask;
            });
            using (LogContext.PushProperty(CorrelationContext.LogProperty, id))
            {
                await next(context);
            }
        });

    /// <summary>
    /// Cabeceras de seguridad del API. La CSP es estricta (el API solo devuelve JSON, PDF y
    /// .xlsx); la documentación de Scalar en desarrollo usa su propia política. Las respuestas
    /// autenticadas no se guardan en caché (contienen datos financieros).
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                headers["Cross-Origin-Resource-Policy"] = "same-site";
                var path = context.Request.Path;
                if (!path.StartsWithSegments("/scalar") && !path.StartsWithSegments("/openapi"))
                {
                    headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
                }

                if (path.StartsWithSegments("/api") && !headers.ContainsKey("Cache-Control"))
                {
                    headers.CacheControl = "no-store";
                }

                return Task.CompletedTask;
            });
            await next(context);
        });
}
