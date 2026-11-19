namespace Opsynex.Infrastructure.Parsing;

/// <summary>Contenido de una hoja como cuadrícula de valores (filas y columnas desde 0).</summary>
public sealed class SheetGrid(string name, IReadOnlyList<object?[]> rows)
{
    public string Name { get; } = name;

    public IReadOnlyList<object?[]> Rows { get; } = rows;

    public int RowCount => Rows.Count;

    public bool IsEmpty => Rows.All(r => r.All(IsBlank));

    public object? Cell(int row, int col) =>
        row >= 0 && row < Rows.Count && col >= 0 && col < Rows[row].Length ? Rows[row][col] : null;

    /// <summary>Texto de la celda si es una cadena no vacía.</summary>
    public string? Text(int row, int col) =>
        Cell(row, col) is string s && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

    public int ColumnCount(int row) => row >= 0 && row < Rows.Count ? Rows[row].Length : 0;

    /// <summary>Celdas de texto de una fila: (columna, texto).</summary>
    public IEnumerable<(int Col, string Text)> TextCells(int row)
    {
        for (var col = 0; col < ColumnCount(row); col++)
        {
            if (Text(row, col) is { } text)
            {
                yield return (col, text);
            }
        }
    }

    public static bool IsBlank(object? value) => value is null || (value is string s && string.IsNullOrWhiteSpace(s));

    /// <summary>Letra de columna de Excel (0 → A, 26 → AA).</summary>
    public static string ColumnLetter(int index)
    {
        var letters = string.Empty;
        for (var n = index + 1; n > 0; n = (n - 1) / 26)
        {
            letters = (char)('A' + ((n - 1) % 26)) + letters;
        }

        return letters;
    }
}
