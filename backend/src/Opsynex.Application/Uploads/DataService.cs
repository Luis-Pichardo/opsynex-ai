using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Abstractions.Security;
using Opsynex.Application.Abstractions.Tenancy;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Uploads;

/// <summary>Tipo de datos que devuelve <c>GET /data</c>.</summary>
public enum DataType
{
    /// <summary>Valores en bruto de la carga más reciente (antes de homologar).</summary>
    Raw,

    /// <summary>Datos financieros consolidados (se llenan con la homologación, etapa 04).</summary>
    Financial,

    Operational,

    /// <summary>Serie mensual de ventas y compras.</summary>
    Monthly,
}

public sealed record DataQuery(int? Year, DataType Type, SheetType? SheetType, Guid? UploadId);

public sealed record RawLineDto(
    string Sheet,
    SheetType SheetType,
    int Row,
    string? Section,
    string OriginalLabel,
    string NormalizedLabel,
    string? AccountCode,
    RowKind RowKind,
    IReadOnlyDictionary<int, decimal> Values);

public sealed record MonthlyDto(int Year, int Month, decimal? Revenue, decimal? Purchases);

public sealed record OperationalDto(int Year, decimal? UnitsProduced, int? EmployeeCount, decimal? ScrapEstimate,
    decimal? EnergyCost, decimal? OperationalCosts, decimal? EnergyConsumptionKwh);

/// <summary>Respuesta de <c>GET /data</c>: solo se llena la lista del tipo pedido.</summary>
public sealed record DataResponse(
    Guid CompanyId,
    DataType Type,
    int? Year,
    Guid? UploadId,
    IReadOnlyList<int> AvailableYears,
    IReadOnlyList<RawLineDto>? Raw = null,
    IReadOnlyList<FinancialData>? Financial = null,
    IReadOnlyList<OperationalDto>? Operational = null,
    IReadOnlyList<MonthlyDto>? Monthly = null);

/// <summary>Consulta de los datos cargados de una empresa.</summary>
public sealed class DataService(
    IRepository<Upload> uploads,
    IRepository<RawAccountValue> rawValues,
    ICompanyYearRepository<FinancialData> financial,
    ICompanyYearRepository<OperationalData> operational,
    IRepository<MonthlyFinancialData> monthly,
    ICurrentUser currentUser,
    ICurrentTenant tenant)
{
    public async Task<DataResponse> GetAsync(DataQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var companyId = (currentUser.IsAdmin ? tenant.CompanyId : currentUser.CompanyId)
            ?? throw new BusinessValidationException(
                "data.company_required", "companyId", "Indica la empresa (cabecera X-Company-Id o parámetro companyId).");

        var companyUploads = (await uploads.ListAsync(u => u.CompanyId == companyId && u.Status != UploadStatus.Failed, cancellationToken))
            .OrderByDescending(u => u.CreatedAt)
            .ToList();
        var years = companyUploads.SelectMany(u => u.Years).Distinct().Order().ToList();
        var response = new DataResponse(companyId, query.Type, query.Year, null, years);

        switch (query.Type)
        {
            case DataType.Financial:
                var rows = await financial.ListByCompanyAsync(companyId, cancellationToken);
                return response with { Financial = rows.Where(r => query.Year is null || r.Year == query.Year).ToList() };

            case DataType.Operational:
                var ops = await operational.ListByCompanyAsync(companyId, cancellationToken);
                return response with
                {
                    Operational = ops.Where(o => query.Year is null || o.Year == query.Year)
                        .Select(o => new OperationalDto(o.Year, o.UnitsProduced, o.EmployeeCount, o.ScrapEstimate, o.EnergyCost,
                            o.OperationalCosts, o.EnergyConsumptionKwh))
                        .ToList(),
                };

            case DataType.Monthly:
                var months = await monthly.ListAsync(m => m.CompanyId == companyId && (query.Year == null || m.Year == query.Year), cancellationToken);
                return response with
                {
                    Monthly = months.OrderBy(m => m.Year).ThenBy(m => m.Month)
                        .Select(m => new MonthlyDto(m.Year, m.Month, m.Revenue, m.Purchases)).ToList(),
                };

            default:
                var upload = query.UploadId is { } uploadId
                    ? companyUploads.FirstOrDefault(u => u.Id == uploadId) ?? throw new NotFoundException("la carga", uploadId)
                    : companyUploads.FirstOrDefault(u => query.Year is null || u.Years.Contains(query.Year.Value));
                if (upload is null)
                {
                    return response with { Raw = [] };
                }

                var sheetType = query.SheetType;
                var values = await rawValues.ListAsync(
                    r => r.UploadId == upload.Id
                        && (query.Year == null || r.Year == query.Year)
                        && (sheetType == null || r.SheetType == sheetType),
                    cancellationToken);
                var lines = values
                    .GroupBy(r => (r.SheetName, r.RowNumber, r.AccountCode))
                    .Select(g =>
                    {
                        var first = g.First();
                        return new RawLineDto(first.SheetName, first.SheetType, first.RowNumber, first.Section, first.OriginalLabel,
                            first.NormalizedLabel, first.AccountCode, first.RowKind, g.ToDictionary(r => r.Year, r => r.Value));
                    })
                    .OrderBy(l => l.SheetType)
                    .ThenBy(l => l.Sheet, StringComparer.Ordinal)
                    .ThenBy(l => l.Row)
                    .ToList();
                return response with { UploadId = upload.Id, Raw = lines };
        }
    }
}
