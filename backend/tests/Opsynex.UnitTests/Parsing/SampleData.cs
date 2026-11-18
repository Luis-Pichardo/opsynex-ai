using System.Text.Json;
using Microsoft.Extensions.Options;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Enums;
using Opsynex.Infrastructure.Parsing;

namespace Opsynex.UnitTests.Parsing;

/// <summary>Acceso a los libros sintéticos de <c>backend/sample-data</c> y a sus valores esperados.</summary>
public static class SampleData
{
    public static readonly string Directory = Path.Combine(AppContext.BaseDirectory, "sample-data");

    private static readonly Lazy<JsonDocument> Manifest = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Directory, "expected_values.json"))));

    public static JsonElement Expected(string file) => Manifest.Value.RootElement.GetProperty(file);

    /// <summary>Valor esperado (RD$) de un concepto del generador para un año.</summary>
    public static decimal Value(string file, int year, string key) =>
        Expected(file).GetProperty("values").GetProperty(year.ToString(System.Globalization.CultureInfo.InvariantCulture)).GetProperty(key).GetDecimal();

    public static ParsedWorkbook Parse(string file, UploadFileType? hint = null)
    {
        using var stream = File.OpenRead(Path.Combine(Directory, file));
        var parser = new WorkbookParser(Options.Create(new UploadOptions()), TimeProvider.System);
        return parser.Parse(stream, file, hint, CancellationToken.None);
    }

    public static ParsedLine Line(this ParsedWorkbook workbook, string sheet, string label, string? sectionContains = null) =>
        workbook.Lines.Single(l => l.SheetName == sheet
            && l.NormalizedLabel == label
            && (sectionContains is null || (l.Section ?? string.Empty).Contains(sectionContains, StringComparison.OrdinalIgnoreCase)));
}
