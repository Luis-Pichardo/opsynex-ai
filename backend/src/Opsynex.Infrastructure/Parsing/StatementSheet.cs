using Opsynex.Application.Uploads;
using Opsynex.Domain.Enums;

namespace Opsynex.Infrastructure.Parsing;

/// <summary>Estado financiero extraído de una hoja: años, columnas de valores y filas.</summary>
public sealed class StatementSheet
{
    public required string SheetName { get; init; }

    public required SheetType SheetType { get; init; }

    /// <summary>Año → columna (desde 0) de sus valores.</summary>
    public required IReadOnlyDictionary<int, int> YearColumns { get; init; }

    /// <summary>Filas en orden, incluidos los títulos (para validar subtotales por grupo).</summary>
    public List<StatementEntry> Entries { get; } = [];

    public IEnumerable<ParsedLine> Lines => Entries.Where(e => e.Line is not null).Select(e => e.Line!);

    public IReadOnlyList<int> Years => YearColumns.Keys.Order().ToList();

    public string ColumnLetter(int year) => SheetGrid.ColumnLetter(YearColumns[year]);
}

/// <summary>Una fila del estado: un título (corta grupos; si está en la col. A, abre una sección) o una línea.</summary>
public sealed record StatementEntry(int RowNumber, string? Section, ParsedLine? Line, bool IsTitle, bool IsSectionTitle);
