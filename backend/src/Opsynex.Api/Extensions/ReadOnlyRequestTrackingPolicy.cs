using Opsynex.Application.Abstractions.Persistence;

namespace Opsynex.Api.Extensions;

/// <summary>Las peticiones GET y HEAD solo leen: EF Core no sigue los cambios de sus entidades.</summary>
internal sealed class ReadOnlyRequestTrackingPolicy(IHttpContextAccessor accessor) : IQueryTrackingPolicy
{
    public bool PreferNoTracking =>
        accessor.HttpContext?.Request.Method is { } method && (HttpMethods.IsGet(method) || HttpMethods.IsHead(method));
}
