using System.Text.Json.Nodes;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Analysis;

/// <summary>Pérdida estimada de una categoría de impacto.</summary>
public sealed record ImpactCategoryDto(ImpactCategory Category, decimal EstimatedLoss, int Problems, decimal? Share);

/// <summary>Problema del top por impacto.</summary>
public sealed record ImpactTopItemDto(
    Guid Id,
    int Year,
    string Code,
    string Title,
    Severity Severity,
    ImpactCategory ImpactCategory,
    decimal ImpactValue,
    decimal? AffectedMarginPp);

/// <summary>Resumen del impacto financiero estimado de un año.</summary>
/// <param name="CompanyId">Empresa.</param>
/// <param name="Year">Año resumido (el último con datos si no se indica); <c>null</c> si no hay análisis.</param>
/// <param name="Currency">Moneda de los montos.</param>
/// <param name="TotalEstimatedLoss">Suma de las pérdidas estimadas de los problemas del año.</param>
/// <param name="Revenue">Ingresos del año.</param>
/// <param name="LossPercentOfRevenue">Pérdida total / ingresos × 100.</param>
/// <param name="QuantifiedProblems">Problemas con pérdida estimada.</param>
/// <param name="UnquantifiedProblems">Problemas sin pérdida estimada (alertas financieras o datos faltantes).</param>
/// <param name="ByCategory">Desglose por categoría, de mayor a menor pérdida.</param>
/// <param name="Top">Los 5 problemas de mayor impacto.</param>
public sealed record ImpactSummaryDto(
    Guid CompanyId,
    int? Year,
    string Currency,
    decimal TotalEstimatedLoss,
    decimal? Revenue,
    decimal? LossPercentOfRevenue,
    int QuantifiedProblems,
    int UnquantifiedProblems,
    IReadOnlyList<ImpactCategoryDto> ByCategory,
    IReadOnlyList<ImpactTopItemDto> Top);

/// <summary>Resumen del impacto financiero estimado (sección 12).</summary>
public sealed class ImpactService(
    CompanyScope scope,
    ICompanyRepository companies,
    IRepository<Problem> problems,
    ICompanyYearRepository<FinancialData> financialData)
{
    public const int TopCount = 5;

    public async Task<ImpactSummaryDto> GetSummaryAsync(int? year, CancellationToken cancellationToken)
    {
        var companyId = scope.Require("impact.company_required");
        if (year is < 1900 or > 2200)
        {
            throw new BusinessValidationException("impact.invalid_year", "year", "El año debe estar entre 1900 y 2200.");
        }

        var company = await companies.GetByIdAsync(companyId, cancellationToken) ?? throw new NotFoundException("la empresa", companyId);
        var financial = await financialData.ListByCompanyAsync(companyId, cancellationToken);
        var target = year ?? (financial.Count == 0 ? null : financial.Max(f => f.Year));
        if (target is null)
        {
            return new ImpactSummaryDto(companyId, null, company.Currency, 0m, null, null, 0, 0, [], []);
        }

        var items = await problems.ListAsync(p => p.CompanyId == companyId && p.Year == target, cancellationToken);
        var revenue = financial.FirstOrDefault(f => f.Year == target)?.Revenue;
        var total = items.Sum(p => p.ImpactValue ?? 0m);

        var byCategory = items
            .GroupBy(p => p.ImpactCategory)
            .Select(g =>
            {
                var loss = g.Sum(p => p.ImpactValue ?? 0m);
                return new ImpactCategoryDto(g.Key, loss, g.Count(), total > 0 ? decimal.Round(loss / total * 100m, 2) : null);
            })
            .OrderByDescending(c => c.EstimatedLoss)
            .ThenBy(c => c.Category)
            .ToList();

        var top = items.Where(p => p.ImpactValue > 0)
            .OrderByDescending(p => p.ImpactValue)
            .ThenByDescending(p => p.Severity)
            .ThenBy(p => p.Code, StringComparer.Ordinal)
            .Take(TopCount)
            .Select(p => new ImpactTopItemDto(p.Id, p.Year, p.Code, p.Title, p.Severity, p.ImpactCategory, p.ImpactValue!.Value,
                p.Evidence["impact"]?["affectedMarginPp"] is JsonValue pp ? pp.GetValue<decimal>() : null))
            .ToList();

        return new ImpactSummaryDto(
            companyId,
            target,
            company.Currency,
            total,
            revenue,
            revenue is > 0 ? decimal.Round(total / revenue.Value * 100m, 2) : null,
            items.Count(p => p.ImpactValue > 0),
            items.Count(p => p.ImpactValue is null or 0),
            byCategory,
            top);
    }
}
