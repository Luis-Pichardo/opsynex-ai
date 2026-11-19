using System.Globalization;
using Opsynex.Application.Common.Exceptions;

namespace Opsynex.Api.Extensions;

/// <summary>
/// Paginación de los listados que devuelven un arreglo (usuarios, empresas, cargas y mapeos):
/// <c>?page=1&amp;pageSize=100</c> (máximo 500). El total va en <c>X-Total-Count</c> y la página en
/// <c>X-Page</c> / <c>X-Page-Size</c>, así el cuerpo sigue siendo un arreglo.
/// </summary>
public static class Paging
{
    public const int DefaultPageSize = 100;
    public const int MaxPageSize = 500;
    public const string TotalCountHeader = "X-Total-Count";

    public static IReadOnlyList<T> Page<T>(this HttpResponse response, IReadOnlyList<T> items, int? page, int? pageSize)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(items);
        var p = page ?? 1;
        var size = pageSize ?? DefaultPageSize;
        if (p < 1)
        {
            throw new BusinessValidationException("paging.invalid_page", "page", "La página debe ser 1 o mayor.");
        }

        if (size is < 1 or > MaxPageSize)
        {
            throw new BusinessValidationException("paging.invalid_page_size", "pageSize", $"El tamaño de página debe estar entre 1 y {MaxPageSize}.");
        }

        response.Headers[TotalCountHeader] = items.Count.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Page"] = p.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Page-Size"] = size.ToString(CultureInfo.InvariantCulture);
        return items.Skip((p - 1) * size).Take(size).ToList();
    }
}
