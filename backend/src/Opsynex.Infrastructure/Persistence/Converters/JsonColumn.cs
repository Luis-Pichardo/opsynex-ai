using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Opsynex.Infrastructure.Persistence.Converters;

/// <summary>
/// Columnas JSON (nvarchar(max) en SQL Server 2022) con System.Text.Json.
/// Incluye un <see cref="ValueComparer"/> para que EF detecte cambios dentro del objeto.
/// </summary>
internal static class JsonColumn
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        // Enums como texto ("Up", "BalanceSheet"); los números guardados antes se siguen leyendo.
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>Mapea una lista como arreglo JSON.</summary>
    public static PropertyBuilder<IList<T>> HasJsonListConversion<T>(this PropertyBuilder<IList<T>> builder) =>
        builder.HasConversion(
            new ValueConverter<IList<T>, string>(
                v => JsonSerializer.Serialize(v, Options),
                v => Deserialize<List<T>>(v) ?? new List<T>()),
            new ValueComparer<IList<T>>(
                (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
                v => JsonSerializer.Serialize(v, Options).GetHashCode(StringComparison.Ordinal),
                v => Deserialize<List<T>>(JsonSerializer.Serialize(v, Options)) ?? new List<T>()))
        .IsRequired();

    /// <summary>Mapea un diccionario como objeto JSON.</summary>
    public static PropertyBuilder<IDictionary<string, TValue>> HasJsonDictionaryConversion<TValue>(
        this PropertyBuilder<IDictionary<string, TValue>> builder) =>
        builder.HasConversion(
            new ValueConverter<IDictionary<string, TValue>, string>(
                v => JsonSerializer.Serialize(v, Options),
                v => Deserialize<Dictionary<string, TValue>>(v) ?? new Dictionary<string, TValue>()),
            new ValueComparer<IDictionary<string, TValue>>(
                (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
                v => JsonSerializer.Serialize(v, Options).GetHashCode(StringComparison.Ordinal),
                v => Deserialize<Dictionary<string, TValue>>(JsonSerializer.Serialize(v, Options)) ?? new Dictionary<string, TValue>()))
        .IsRequired();

    /// <summary>Mapea un objeto (record) como objeto JSON.</summary>
    public static PropertyBuilder<T> HasJsonValueConversion<T>(this PropertyBuilder<T> builder)
        where T : class, new() =>
        builder.HasConversion(
            new ValueConverter<T, string>(
                v => JsonSerializer.Serialize(v, Options),
                v => Deserialize<T>(v) ?? new T()),
            new ValueComparer<T>(
                (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
                v => JsonSerializer.Serialize(v, Options).GetHashCode(StringComparison.Ordinal),
                v => Deserialize<T>(JsonSerializer.Serialize(v, Options)) ?? new T()))
        .IsRequired();

    /// <summary>Mapea un <see cref="JsonObject"/> libre.</summary>
    public static PropertyBuilder<JsonObject> HasJsonObjectConversion(this PropertyBuilder<JsonObject> builder) =>
        builder.HasConversion(
            new ValueConverter<JsonObject, string>(
                v => v.ToJsonString(Options),
                v => ParseObject(v)),
            new ValueComparer<JsonObject>(
                (a, b) => JsonNode.DeepEquals(a, b),
                v => v.ToJsonString(Options).GetHashCode(StringComparison.Ordinal),
                v => ParseObject(v.ToJsonString(Options))))
        .IsRequired();

    private static TResult? Deserialize<TResult>(string json) =>
        string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<TResult>(json, Options);

    private static JsonObject ParseObject(string json) =>
        string.IsNullOrWhiteSpace(json) ? new JsonObject() : JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
}
