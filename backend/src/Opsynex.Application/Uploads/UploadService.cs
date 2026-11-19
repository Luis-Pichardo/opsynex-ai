using Microsoft.Extensions.Options;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Abstractions.Security;
using Opsynex.Application.Abstractions.Tenancy;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Uploads;

/// <summary>
/// Carga de archivos: valida, analiza el libro y guarda el reporte y los valores en bruto
/// (filas de los estados, serie mensual y datos operacionales). El archivo original no se guarda.
/// </summary>
public sealed class UploadService(
    IWorkbookParser parser,
    IRepository<Upload> uploads,
    IRepository<RawAccountValue> rawValues,
    IRepository<MonthlyFinancialData> monthly,
    ICompanyYearRepository<OperationalData> operational,
    ICompanyRepository companies,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ICurrentTenant tenant,
    Homologation.UploadProcessingService processing,
    IOptions<UploadOptions> options)
{
    private const int PreviewLinesPerSheet = 40;
    private const string UnlabeledSubtotal = "(subtotal sin etiqueta)";

    /// <summary>Procesa un archivo. Devuelve el reporte; <c>Status = Failed</c> si hubo errores bloqueantes.</summary>
    public async Task<UploadReportDto> UploadAsync(UploadFileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var companyId = await ResolveCompanyAsync(request.CompanyId, cancellationToken);
        var settings = options.Value;

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!UploadOptions.AllowedExtensions.Contains(extension))
        {
            throw new BusinessValidationException(
                "upload.unsupported_extension", "file", $"Formato no admitido ({extension}). Sube un archivo .xls, .xlsx o .csv.");
        }

        if (request.Length > settings.MaxBytes)
        {
            throw new PayloadTooLargeException(
                "upload.too_large", $"El archivo pesa más de {settings.MaxMb} MB. Reduce su tamaño o sube solo las hojas necesarias.");
        }

        var parsed = parser.Parse(request.Content, request.FileName, request.FileType, cancellationToken);
        var upload = new Upload
        {
            CompanyId = companyId,
            UserId = currentUser.UserId ?? throw new ForbiddenException("auth.unauthorized", "Inicia sesión para continuar."),
            FileName = Truncate(Path.GetFileName(request.FileName), 260),
            FileType = parsed.HasBlockingErrors ? request.FileType ?? UploadFileType.AccountingWorkbook : parsed.FileType,
            Status = parsed.HasBlockingErrors ? UploadStatus.Failed : UploadStatus.Validated,
            FileSizeBytes = request.Length,
            Errors = parsed.Errors,
            Warnings = parsed.Warnings,
            Sheets = parsed.Sheets,
            Years = parsed.Years,
            Summary = BuildSummary(parsed),
        };
        await uploads.AddAsync(upload, cancellationToken);

        if (!parsed.HasBlockingErrors)
        {
            var values = ToRawValues(parsed, upload).ToList();
            await rawValues.AddRangeAsync(values, cancellationToken);
            upload.RowsCount = values.Count;
            await UpsertMonthlyAsync(parsed, upload, cancellationToken);
            await UpsertOperationalAsync(parsed, upload, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Análisis automático: homologar con las mejores sugerencias y consolidar.
        if (upload.Status == UploadStatus.Validated)
        {
            await processing.ProcessAsync(upload.Id, cancellationToken);
        }

        return ToReport(upload, Preview(parsed.Lines));
    }

    public async Task<IReadOnlyList<UploadListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        var list = await uploads.ListAsync(cancellationToken);
        var names = await users.GetNamesIgnoringFiltersAsync(list.Select(u => u.UserId), cancellationToken);
        return list
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new UploadListItemDto(u.Id, u.CompanyId, u.FileName, u.FileType, u.Status, [.. u.Years], u.RowsCount,
                u.Warnings.Count, u.Errors.Count, u.CreatedAt, u.AnalysisStatus, u.NeedsReview, names.GetValueOrDefault(u.UserId)))
            .ToList();
    }

    public async Task<UploadReportDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var upload = await uploads.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("la carga", id);
        var previewTypes = PreviewSheetTypes;
        var raw = await rawValues.ListAsync(r => r.UploadId == id && previewTypes.Contains(r.SheetType), cancellationToken);
        var lines = raw
            .GroupBy(r => (r.SheetName, r.RowNumber))
            .Select(g =>
            {
                var first = g.First();
                return new ParsedLine(first.SheetName, first.SheetType, first.RowNumber, first.Section, first.OriginalLabel,
                    first.NormalizedLabel, first.AccountCode, first.RowKind, g.ToDictionary(r => r.Year, r => r.Value));
            })
            .OrderBy(l => l.SheetType)
            .ThenBy(l => l.RowNumber)
            .ToList();
        return ToReport(upload, Preview(lines));
    }

    private static readonly SheetType[] PreviewSheetTypes =
    [
        SheetType.IncomeStatement, SheetType.BalanceSheet, SheetType.CashFlow, SheetType.CostStructure, SheetType.Operational,
    ];

    private async Task<Guid> ResolveCompanyAsync(Guid? requested, CancellationToken cancellationToken)
    {
        Guid? companyId = currentUser.IsAdmin ? requested ?? tenant.CompanyId : currentUser.CompanyId;
        if (companyId is not { } id)
        {
            throw new BusinessValidationException(
                "upload.company_required", "companyId", "Indica la empresa a la que pertenece el archivo (companyId).");
        }

        if (!await companies.AnyAsync(c => c.Id == id, cancellationToken))
        {
            throw new BusinessValidationException("upload.company_not_found", "companyId", $"No existe la empresa «{id}».");
        }

        return id;
    }

    private static UploadSummary BuildSummary(ParsedWorkbook parsed) => new()
    {
        EmployeeCount = parsed.Employees?.Count,
        EmployeesByDepartment = parsed.Employees?.ByDepartment ?? [],
        AnnualPayroll = parsed.Employees?.Payroll,
        MonthlyRecords = parsed.Monthly.Count,
        TrialBalanceReliable = parsed.TrialBalance?.Reliable,
        TrialBalanceDifference = parsed.TrialBalance?.Difference,
    };

    private static IEnumerable<RawAccountValue> ToRawValues(ParsedWorkbook parsed, Upload upload) =>
        parsed.Lines.SelectMany(line => line.Values.Select(value => new RawAccountValue
        {
            UploadId = upload.Id,
            CompanyId = upload.CompanyId,
            ClientAccount = Truncate(line.NormalizedLabel.Length > 0 ? line.NormalizedLabel : UnlabeledSubtotal, 300),
            Year = value.Key,
            Value = value.Value,
            SheetName = Truncate(line.SheetName, 100),
            SheetType = line.SheetType,
            RowNumber = line.RowNumber,
            Section = line.Section is null ? null : Truncate(line.Section, 300),
            OriginalLabel = Truncate(line.OriginalLabel, 500),
            NormalizedLabel = Truncate(line.NormalizedLabel, 300),
            AccountCode = line.AccountCode is null ? null : Truncate(line.AccountCode, 32),
            RowKind = line.RowKind,
        }));

    /// <summary>La serie mensual reemplaza los meses que ya existían para la empresa.</summary>
    private async Task UpsertMonthlyAsync(ParsedWorkbook parsed, Upload upload, CancellationToken cancellationToken)
    {
        if (parsed.Monthly.Count == 0)
        {
            return;
        }

        var years = parsed.Monthly.Select(m => m.Year).Distinct().ToList();
        var existing = await monthly.ListAsync(m => m.CompanyId == upload.CompanyId && years.Contains(m.Year), cancellationToken);
        foreach (var value in parsed.Monthly)
        {
            var row = existing.FirstOrDefault(m => m.Year == value.Year && m.Month == value.Month);
            if (row is null)
            {
                row = new MonthlyFinancialData { CompanyId = upload.CompanyId, Year = value.Year, Month = value.Month };
                await monthly.AddAsync(row, cancellationToken);
            }

            row.Revenue = value.Revenue;
            row.Purchases = value.Purchases;
            row.UploadId = upload.Id;
        }
    }

    /// <summary>Actualiza solo los campos operacionales que trae el archivo (no borra los demás).</summary>
    private async Task UpsertOperationalAsync(ParsedWorkbook parsed, Upload upload, CancellationToken cancellationToken)
    {
        foreach (var group in parsed.Operational.GroupBy(o => o.Year))
        {
            var row = await operational.GetByCompanyAndYearAsync(upload.CompanyId, group.Key, cancellationToken);
            if (row is null)
            {
                row = new OperationalData { CompanyId = upload.CompanyId, Year = group.Key };
                await operational.AddAsync(row, cancellationToken);
            }

            foreach (var value in group)
            {
                switch (value.Field)
                {
                    case OperationalField.UnitsProduced: row.UnitsProduced = value.Value; break;
                    case OperationalField.EmployeeCount: row.EmployeeCount = (int)decimal.Round(value.Value); break;
                    case OperationalField.EnergyConsumptionKwh: row.EnergyConsumptionKwh = value.Value; break;
                    case OperationalField.ScrapEstimate: row.ScrapEstimate = value.Value; break;
                    case OperationalField.OperationalCosts: row.OperationalCosts = value.Value; break;
                    case OperationalField.EnergyCost: row.EnergyCost = value.Value; break;
                }
            }

            row.UploadId = upload.Id;
        }
    }

    private static UploadPreview Preview(IEnumerable<ParsedLine> lines) => new(
        lines.Where(l => PreviewSheetTypes.Contains(l.SheetType))
            .GroupBy(l => l.SheetName)
            .OrderBy(g => g.First().SheetType)
            .SelectMany(g => g.Take(PreviewLinesPerSheet))
            .Select(l => new PreviewLine(l.SheetName, l.SheetType, l.RowNumber, l.Section,
                l.NormalizedLabel.Length > 0 ? l.NormalizedLabel : UnlabeledSubtotal, l.RowKind, l.Values))
            .ToList());

    private static UploadReportDto ToReport(Upload upload, UploadPreview preview) => new(
        upload.Id, upload.CompanyId, upload.FileName, upload.FileType, upload.Status, [.. upload.Years], [.. upload.Sheets],
        [.. upload.Errors], [.. upload.Warnings], upload.Summary, upload.RowsCount, preview, upload.CreatedAt,
        upload.AnalysisStatus, upload.NeedsReview, upload.DoubtfulAccounts);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
