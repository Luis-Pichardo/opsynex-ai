using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using Opsynex.Application.Uploads;
using Opsynex.Infrastructure.Parsing;

namespace Opsynex.UnitTests.Parsing;

/// <summary>Los libros con macros VBA se rechazan (etapa 13); los normales se leen igual.</summary>
public sealed class MacroDetectionTests
{
    private static byte[] Workbook(bool withMacros)
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            workbook.AddWorksheet("Hoja1").Cell(1, 1).Value = "Ventas netas";
            workbook.SaveAs(stream);
        }

        if (withMacros)
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true);
            using var entry = zip.CreateEntry("xl/vbaProject.bin").Open();
            entry.Write([1, 2, 3]);
        }

        return stream.ToArray();
    }

    private static ParsedWorkbook Parse(byte[] content, string fileName)
    {
        using var stream = new MemoryStream(content);
        return new WorkbookParser(Options.Create(new UploadOptions()), TimeProvider.System).Parse(stream, fileName, null, CancellationToken.None);
    }

    [Fact]
    public void XlsxWithVbaProject_IsRejected()
    {
        var result = Parse(Workbook(withMacros: true), "libro.xlsx");

        result.Errors.Single().Code.ShouldBe("upload.macros_not_allowed");
        result.Sheets.ShouldBeEmpty();
    }

    [Fact]
    public void XlsWithVbaStorage_IsDetected()
    {
        byte[] ole2 = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, .. new byte[100], .. Encoding.Unicode.GetBytes("_VBA_PROJECT_CUR")];

        WorkbookReader.HasMacros(ole2, isZip: false).ShouldBeTrue();
        WorkbookReader.HasMacros(File.ReadAllBytes(Path.Combine(SampleData.Directory, "libro_sintetico_2a.xls")), isZip: false).ShouldBeFalse();
    }

    [Fact]
    public void WorkbookWithoutMacros_IsNotFlagged()
    {
        WorkbookReader.HasMacros(Workbook(withMacros: false), isZip: true).ShouldBeFalse();
        WorkbookReader.HasMacros([0x50, 0x4B, 0x03, 0x04, 0, 0], isZip: true).ShouldBeFalse(); // ZIP dañado: lo reporta el lector
    }
}
