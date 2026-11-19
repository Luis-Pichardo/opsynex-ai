using Opsynex.Application.Abstractions.Diagnostics;

namespace Opsynex.Infrastructure.AiService;

/// <summary>Propaga el <c>X-Correlation-Id</c> de la operación actual a las llamadas al servicio de IA.</summary>
internal sealed class CorrelationIdHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (CorrelationContext.Current is { } id && !request.Headers.Contains(CorrelationContext.HeaderName))
        {
            request.Headers.Add(CorrelationContext.HeaderName, id);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
