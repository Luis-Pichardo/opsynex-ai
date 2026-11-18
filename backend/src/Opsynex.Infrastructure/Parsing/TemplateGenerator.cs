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
}
