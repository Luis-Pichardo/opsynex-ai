using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Enums;
using Opsynex.Infrastructure.Parsing;
using Opsynex.UnitTests.Parsing;

namespace Opsynex.UnitTests.Industrial;

/// <summary>Lectura de las tablas industriales: detección por encabezados, validación fila por fila y plantillas.</summary>
public sealed class IndustrialTableExtractorTests
{
    private static ParsedWorkbook Parse(string file, UploadFileType? hint = null) => SampleData.Parse(Path.Combine("industrial", file), hint);

    private static ParsedWorkbook Parse(byte[] content, string fileName, UploadFileType? hint = null)
    {
        using var stream = new MemoryStream(content);
        return new WorkbookParser(Options.Create(new UploadOptions()), TimeProvider.System).Parse(stream, fileName, hint, CancellationToken.None);
    }

    [Fact]
    public void Production_IsDetectedByItsHeaders()
    {
        var result = Parse("produccion.xlsx");

        result.Errors.ShouldBeEmpty();
        result.Warnings.ShouldBeEmpty();
        result.FileType.ShouldBe(UploadFileType.Production);
        result.Years.ShouldBe([2024]);
        result.Lines.ShouldBeEmpty();
        var production = result.Industrial!.Production;
        production.Count.ShouldBe(234); // 78 días hábiles × 3 máquinas
        production.Select(p => p.MachineId).Distinct().Order().ShouldBe(["MAQ-01", "MAQ-02", "MAQ-03"]);
        var first = production[0];
        first.Date.ShouldBe(new DateOnly(2024, 10, 1));
        first.LineId.ShouldBe("LINEA-1");
        first.PlannedMinutes.ShouldBe(960m);
        first.CycleTime.ShouldNotBeNull();
        result.Sheets.Single().DetectedType.ShouldBe(SheetType.Production);
    }

    [Fact]
    public void CostsAndStandards_AreDetectedByTheirHeaders()
    {
        var costs = Parse("costos.xlsx");
        costs.FileType.ShouldBe(UploadFileType.Costs);
        costs.Industrial!.Costs.Count.ShouldBe(9);
        costs.Industrial.Costs.ShouldAllBe(c => c.Total > 0m);

        var standards = Parse("estandares.xlsx");
        standards.FileType.ShouldBe(UploadFileType.Standards);
        standards.Years.ShouldBeEmpty();
        var maq3 = standards.Industrial!.Standards.Single(s => s.MachineId == "MAQ-03");
        maq3.StandardCycleTime.ShouldBe(36m);
        maq3.StandardCost.ShouldBe(41m);
        maq3.CostPerMinute.ShouldBe(42m);
        maq3.ProcessName.ShouldBe("Ensamble");
    }

    [Fact]
    public void InvalidRows_AreSkipped_WithSheetRowAndColumn()
    {
        var result = Parse("produccion_con_errores.csv");

        result.Errors.ShouldBeEmpty();
        result.Industrial!.Production.Count.ShouldBe(2);
        var duplicate = result.Industrial.Production.Single(p => p.Date == new DateOnly(2024, 10, 1));
        duplicate.DowntimeMinutes.ShouldBe(42m); // gana la última fila repetida
        var noProduced = result.Industrial.Production.Single(p => p.Date == new DateOnly(2024, 10, 2));
        noProduced.UnitsProduced.ShouldBe(noProduced.GoodUnits);

        result.Warnings.Select(w => (w.Code, w.Row, w.Column)).ShouldBe(
        [
            ("upload.industrial_invalid_row", 4, "G"),  // buenas > totales
            ("upload.industrial_invalid_row", 5, "B"),  // sin máquina
            ("upload.industrial_invalid_row", 6, "A"),  // fecha inválida
            ("upload.industrial_invalid_row", 7, "E"),  // parada > planificado
            ("upload.industrial_duplicate", 8, null),
        ]);
        result.Warnings.ShouldAllBe(w => w.Message.StartsWith("Fila ", StringComparison.Ordinal) && w.Sheet != null);
    }

    [Fact]
    public void ForcedType_WithoutItsColumns_IsABlockingError()
    {
        var result = Parse("estandares.xlsx", UploadFileType.Production);

        result.Errors.Single().Code.ShouldBe("upload.industrial_columns_missing");
        result.FileType.ShouldBe(UploadFileType.Production);
    }

    [Fact]
    public void AccountingWorkbook_IsNotTakenAsIndustrial()
    {
        var result = SampleData.Parse("libro_sintetico_2a.xls");

        result.Industrial.ShouldBeNull();
        result.FileType.ShouldBe(UploadFileType.AccountingWorkbook);
    }

    [Fact]
    public void OneWorkbook_CanBringSeveralTables_WithTitlesAboveAndEnglishHeaders()
    {
        using var workbook = new XLWorkbook();
        var production = workbook.AddWorksheet("Datos");
        production.Cell(1, 1).Value = "PLANTA FICTICIA — producción de octubre";
        string[] headers = ["date", "machine_id", "line_id", "planned_minutes", "downtime_minutes", "total_units", "good_units", "Operador"];
        for (var i = 0; i < headers.Length; i++)
        {
            production.Cell(3, i + 1).Value = headers[i];
        }

        production.Cell(4, 1).Value = new DateTime(2024, 10, 1);
        production.Cell(4, 2).Value = "maq-9";
        production.Cell(4, 3).Value = "linea-x";
        production.Cell(4, 4).Value = 480;
        production.Cell(4, 5).Value = 30;
        production.Cell(4, 6).Value = 900;
        production.Cell(4, 7).Value = 880;
        production.Cell(4, 8).Value = "No se lee";

        var standards = workbook.AddWorksheet("Estándares");
        standards.Cell(1, 1).Value = "Máquina";
        standards.Cell(1, 2).Value = "Ciclo estándar (s)";
        standards.Cell(2, 1).Value = "MAQ-9";
        standards.Cell(2, 2).Value = 28.5;
        workbook.AddWorksheet("Notas").Cell(1, 1).Value = "Comentarios libres";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var result = Parse(stream.ToArray(), "planta.xlsx");

        result.Errors.ShouldBeEmpty();
        result.FileType.ShouldBe(UploadFileType.Production);
        var record = result.Industrial!.Production.Single();
        record.MachineId.ShouldBe("MAQ-9");     // los códigos se guardan en mayúsculas
        record.LineId.ShouldBe("LINEA-X");
        record.UnitsProduced.ShouldBe(880m);
        result.Industrial.Standards.Single().StandardCycleTime.ShouldBe(28.5m);
        result.Sheets.Select(s => (s.Name, s.DetectedType, s.Ignored)).ShouldBe(
        [
            ("Datos", SheetType.Production, false),
            ("Estándares", SheetType.Standards, false),
            ("Notas", SheetType.Unknown, true),
        ]);
    }

    [Theory]
    [InlineData(UploadFileType.Production)]
    [InlineData(UploadFileType.Costs)]
    [InlineData(UploadFileType.Standards)]
    public void GeneratedTemplates_AreReadBack(UploadFileType fileType)
    {
        var template = IndustrialTemplates.Find(fileType)!;
        var bytes = new TemplateGenerator().Generate(fileType, []);

        var result = Parse(bytes, template.FileName);

        result.Errors.ShouldBeEmpty();
        result.FileType.ShouldBe(fileType);
        result.Industrial!.Count.ShouldBe(template.Example.Count);
        result.Sheets.Single(s => s.Name == "Instrucciones").Ignored.ShouldBeTrue();
    }

    [Fact]
    public void Templates_RequireTheSpecColumns()
    {
        IndustrialTemplates.Production.RequiredColumns.Select(c => c.Key).ShouldBe(
            ["date", "machine_id", "planned_minutes", "downtime_minutes", "total_units", "good_units"]);
        IndustrialTemplates.Standards.RequiredColumns.Select(c => c.Key).ShouldBe(["machine_id", "standard_cycle_time"]);
        IndustrialTemplates.IsIndustrial(UploadFileType.Costs).ShouldBeTrue();
        IndustrialTemplates.IsIndustrial(UploadFileType.IncomeStatement).ShouldBeFalse();
    }
}
