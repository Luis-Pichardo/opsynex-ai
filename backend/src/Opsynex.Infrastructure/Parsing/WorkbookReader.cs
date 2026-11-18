using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Entities;

namespace Opsynex.Infrastructure.Parsing;

/// <summary>
/// Lee .xls (BIFF8), .xlsx y .csv a cuadrículas de valores. Valida la firma real del archivo
/// (no solo la extensión) y aplica los límites de hojas y filas. Lee los valores calculados de
/// las fórmulas, nunca la fórmula.
/// </summary>
public sealed class WorkbookReader(UploadOptions options)
{
    private static readonly byte[] Ole2Signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    static WorkbookReader()
    {
        // Los .xls antiguos usan la página de códigos 1252.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public sealed record Result(List<SheetGrid> Sheets, List<UploadIssue> Errors, List<UploadIssue> Warnings);

    public Result Read(byte[] content, string fileName)
    {
        ArgumentNullException.ThrowIfNull(content);
        var sheets = new List<SheetGrid>();
        var errors = new List<UploadIssue>();
        var warnings = new List<UploadIssue>();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (content.Length == 0)
        {
            errors.Add(new UploadIssue("upload.empty_file", "El archivo está vacío."));
            return new Result(sheets, errors, warnings);
        }

        var isOle2 = StartsWith(content, Ole2Signature);
        var isZip = StartsWith(content, ZipSignature);

        if (extension is ".xls" or ".xlsx")
        {
            if (!isOle2 && !isZip)
            {
                errors.Add(new UploadIssue(
                    "upload.invalid_signature",
                    $"El archivo «{fileName}» no es un libro de Excel válido aunque su extensión sea {extension}."));
                return new Result(sheets, errors, warnings);
            }

            if ((extension == ".xls" && isZip) || (extension == ".xlsx" && isOle2))
            {
                warnings.Add(new UploadIssue(
                    "upload.extension_mismatch",
                    $"La extensión {extension} no coincide con el formato real del archivo; se leyó igualmente."));
            }

            ReadExcel(content, sheets, errors, warnings);
        }
        else if (extension == ".csv")
        {
            if (isOle2 || isZip || content.Take(4096).Contains((byte)0))
            {
                errors.Add(new UploadIssue("upload.invalid_signature", "El archivo .csv no es un archivo de texto."));
                return new Result(sheets, errors, warnings);
            }

            ReadCsv(content, fileName, sheets, warnings);
        }
        else
        {
            errors.Add(new UploadIssue(
                "upload.unsupported_extension",
                $"Formato no admitido ({extension}). Sube un archivo .xls, .xlsx o .csv."));
        }

        return new Result(sheets, errors, warnings);
    }

    private void ReadExcel(byte[] content, List<SheetGrid> sheets, List<UploadIssue> errors, List<UploadIssue> warnings)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            do
            {
                if (sheets.Count >= options.MaxSheets)
                {
                    warnings.Add(new UploadIssue(
                        "upload.too_many_sheets",
                        $"El libro tiene más de {options.MaxSheets} hojas; solo se leyeron las primeras {options.MaxSheets}."));
                    break;
                }

                var rows = new List<object?[]>();
                var truncated = false;
                while (reader.Read())
                {
                    if (rows.Count >= options.MaxRowsPerSheet)
                    {
                        truncated = true;
                        break;
                    }

                    var row = new object?[reader.FieldCount];
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        row[i] = reader.GetValue(i);
                    }

                    rows.Add(row);
                }

                if (truncated)
                {
                    warnings.Add(new UploadIssue(
                        "upload.too_many_rows",
                        $"La hoja tiene más de {options.MaxRowsPerSheet} filas; solo se leyeron las primeras.",
                        reader.Name));
                }

                sheets.Add(new SheetGrid(reader.Name, rows));
            }
            while (reader.NextResult());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            sheets.Clear();
            errors.Add(new UploadIssue(
                "upload.unreadable",
                "No se pudo leer el archivo: está dañado o protegido. Ábrelo en Excel, guárdalo de nuevo y vuelve a subirlo."));
        }
    }

    private void ReadCsv(byte[] content, string fileName, List<SheetGrid> sheets, List<UploadIssue> warnings)
    {
        var text = Decode(content);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            DetectDelimiter = true,
            BadDataFound = null,
            MissingFieldFound = null,
        };

        using var reader = new StringReader(text);
        using var csv = new CsvReader(reader, config);
        var rows = new List<object?[]>();
        while (csv.Read())
        {
            if (rows.Count >= options.MaxRowsPerSheet)
            {
                warnings.Add(new UploadIssue("upload.too_many_rows", $"El archivo tiene más de {options.MaxRowsPerSheet} filas; solo se leyeron las primeras."));
                break;
            }

            var record = csv.Parser.Record ?? [];
            rows.Add(record.Select(v => string.IsNullOrWhiteSpace(v) ? null : (object)v.Trim()).ToArray());
        }

        sheets.Add(new SheetGrid(Path.GetFileNameWithoutExtension(fileName), rows));
    }

    /// <summary>UTF-8 (con o sin BOM) y, si no es válido, Windows-1252 (Excel en español).</summary>
    private static string Decode(byte[] content)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(content).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1252).GetString(content);
        }
    }

    private static bool StartsWith(byte[] content, byte[] signature) =>
        content.Length >= signature.Length && content.AsSpan(0, signature.Length).SequenceEqual(signature);
}
