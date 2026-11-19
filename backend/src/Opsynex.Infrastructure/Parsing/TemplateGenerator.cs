using ClosedXML.Excel;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Enums;

namespace Opsynex.Infrastructure.Parsing;

/// <summary>Genera las plantillas simples .xlsx con ClosedXML (solo para escribir; para leer se usa ExcelDataReader).</summary>
public sealed class TemplateGenerator : IUploadTemplateGenerator
{
    public byte[] Generate(UploadFileType fileType, IReadOnlyList<int> years)
    {
        ArgumentNullException.ThrowIfNull(years);
        if (IndustrialTemplates.Find(fileType) is { } industrial)
        {
            return GenerateIndustrial(industrial);
        }

        var template = UploadTemplates.Find(fileType)
            ?? throw new ArgumentOutOfRangeException(nameof(fileType), fileType, "No hay plantilla para este tipo de archivo.");

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Plantilla");
        sheet.Cell(1, 1).Value = "Nombre de la empresa";
        sheet.Cell(2, 1).Value = template.Title;
        sheet.Cell(2, 1).Style.Font.Bold = true;
        sheet.Cell(3, 1).Value = "Escribe los montos de cada año en las columnas de años. Deja vacío lo que no tengas (no escribas 0).";
        sheet.Cell(3, 1).Style.Font.Italic = true;

        sheet.Cell(5, 1).Value = "Cuenta";
        for (var i = 0; i < years.Count; i++)
        {
            sheet.Cell(5, 2 + i).Value = years[i];
        }

        var header = sheet.Range(5, 1, 5, 1 + years.Count);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF1FB");

        for (var r = 0; r < template.Rows.Count; r++)
        {
            sheet.Cell(6 + r, 1).Value = template.Rows[r];
            for (var i = 0; i < years.Count; i++)
            {
                sheet.Cell(6 + r, 2 + i).Style.NumberFormat.Format = "#,##0.00";
            }
        }

        sheet.Column(1).Width = 42;
        for (var i = 0; i < years.Count; i++)
        {
            sheet.Column(2 + i).Width = 18;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Plantilla tabular industrial: hoja de datos con encabezados y dos filas de ejemplo, y hoja
    /// de instrucciones con la descripción de cada columna.
    /// </summary>
    private static byte[] GenerateIndustrial(IndustrialTemplates.Template template)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(template.SheetName);
        for (var c = 0; c < template.Columns.Count; c++)
        {
            var column = template.Columns[c];
            var cell = sheet.Cell(1, c + 1);
            cell.Value = column.Header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(column.Required ? "#EEF1FB" : "#F5F6F8");
            sheet.Column(c + 1).Width = Math.Max(14, column.Header.Length + 4);
            if (column.Kind == IndustrialTemplates.ColumnKind.Date)
            {
                sheet.Column(c + 1).Style.DateFormat.Format = "yyyy-mm-dd";
            }
        }

        for (var r = 0; r < template.Example.Count; r++)
        {
            var values = template.Example[r];
            for (var c = 0; c < values.Length && c < template.Columns.Count; c++)
            {
                var cell = sheet.Cell(r + 2, c + 1);
                switch (values[c])
                {
                    case string text when template.Columns[c].Kind == IndustrialTemplates.ColumnKind.Date:
                        cell.Value = DateTime.ParseExact(text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case string text:
                        cell.Value = text;
                        break;
                    case int number:
                        cell.Value = number;
                        break;
                    case double number:
                        cell.Value = number;
                        break;
                }
            }
        }

        sheet.SheetView.FreezeRows(1);

        var help = workbook.AddWorksheet("Instrucciones");
        help.Cell(1, 1).Value = template.Title;
        help.Cell(1, 1).Style.Font.Bold = true;
        help.Cell(2, 1).Value = "Una fila por registro. Las filas de ejemplo se pueden borrar. Las columnas en azul son obligatorias; " +
            "no agregues datos personales (nombres de operadores, cédulas).";
        help.Cell(4, 1).Value = "Columna";
        help.Cell(4, 2).Value = "Obligatoria";
        help.Cell(4, 3).Value = "Descripción";
        help.Range(4, 1, 4, 3).Style.Font.Bold = true;
        for (var i = 0; i < template.Columns.Count; i++)
        {
            var column = template.Columns[i];
            help.Cell(5 + i, 1).Value = column.Header;
            help.Cell(5 + i, 2).Value = column.Required ? "Sí" : "No";
            help.Cell(5 + i, 3).Value = column.Help;
        }

        help.Column(1).Width = 28;
        help.Column(2).Width = 12;
        help.Column(3).Width = 80;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
