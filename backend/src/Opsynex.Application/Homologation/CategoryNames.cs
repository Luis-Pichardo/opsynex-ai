using System.Text.Json;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Homologation;

/// <summary>Nombres de categoría del servicio de IA (snake_case) ↔ <see cref="OpsynexCategory"/>.</summary>
public static class CategoryNames
{
    private static readonly Dictionary<string, OpsynexCategory> ByName = Enum.GetValues<OpsynexCategory>()
        .ToDictionary(ToSnakeCase, c => c, StringComparer.OrdinalIgnoreCase);

    public static string ToSnakeCase(OpsynexCategory category) => JsonNamingPolicy.SnakeCaseLower.ConvertName(category.ToString());

    public static OpsynexCategory? Parse(string? name) =>
        name is not null && ByName.TryGetValue(name, out var category) ? category : null;
}
