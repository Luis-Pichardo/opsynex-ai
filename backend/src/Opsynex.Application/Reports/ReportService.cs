using System.Globalization;
using System.Text;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Analysis;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Engines.Diagnosis;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Engines.Scoring;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Reports;

/// <summary>Arma el informe ejecutivo (PDF) de la empresa para un año (por defecto, el último analizado).</summary>
public sealed class ReportService(
    CompanyScope scope,
    ICompanyRepository companies,
    ICompanyYearRepository<KpiResult> kpiResults,
    ICompanyYearRepository<Score> scores,
    ICompanyYearRepository<FinancialData> financialData,
    IRepository<Problem> problems,
    IRepository<Upload> uploads,
    ICompanyYearRepository<ExecutiveSummary> summaries,
    IExecutiveReportRenderer renderer,
    TimeProvider timeProvider)
{
    private static readonly Dictionary<ImpactCategory, string> CategoryNames = new()
    {
        [ImpactCategory.MarginLoss] = "Pérdida de margen",
        [ImpactCategory.ExcessCost] = "Exceso de costos",
        [ImpactCategory.ProductivityLoss] = "Pérdida de productividad",
        [ImpactCategory.OperationalWaste] = "Desperdicio operacional",
        [ImpactCategory.FinancialAlert] = "Alertas financieras",
        [ImpactCategory.Anomaly] = "Anomalías",
    };

    public static string CategoryName(ImpactCategory category) => CategoryNames[category];

    public async Task<ReportFile> ExecutiveAsync(int? year, CancellationToken cancellationToken)
    {
        var model = await BuildAsync(year, cancellationToken);
        return new ReportFile($"informe_{Slug(model.CompanyName)}_{model.Year}.pdf", renderer.Render(model));
    }

    public async Task<ExecutiveReportModel> BuildAsync(int? year, CancellationToken cancellationToken)
    {
        var companyId = scope.Require("reports.company_required");
        if (year is < 1900 or > 2200)
        {
            throw new BusinessValidationException("reports.invalid_year", "year", "El año debe estar entre 1900 y 2200.");
        }

        var company = await companies.GetByIdAsync(companyId, cancellationToken) ?? throw new NotFoundException("la empresa", companyId);
        var kpis = await kpiResults.ListByCompanyAsync(companyId, cancellationToken);
        var current = year is null ? kpis.MaxBy(k => k.Year) : kpis.FirstOrDefault(k => k.Year == year);
        if (current is null)
        {
            throw new BusinessValidationException(
                "reports.no_data", "year",
                year is null ? "La empresa todavía no tiene análisis. Sube sus estados financieros primero." : $"No hay análisis para {year}.");
        }

        var previous = kpis.FirstOrDefault(k => k.Year == current.Year - 1);
        var allScores = (await scores.ListByCompanyAsync(companyId, cancellationToken)).ToDictionary(s => s.Year);
        var financial = await financialData.ListByCompanyAsync(companyId, cancellationToken);
        var yearProblems = await problems.ListAsync(p => p.CompanyId == companyId && p.Year == current.Year, cancellationToken);
        var revenue = financial.FirstOrDefault(f => f.Year == current.Year)?.Revenue;

        var ordered = DiagnosisEngine.Prioritize(yearProblems, p => p.Severity, p => p.ImpactValue)
            .ThenBy(p => p.Code, StringComparer.Ordinal)
            .Select((p, i) => new ReportProblem(i + 1, p.Code, p.Title, p.Severity, p.ImpactCategory, p.ImpactValue, p.Description,
                p.Recommendation, AiText(p.AiRecommendation)))
            .ToList();

        var totalLoss = yearProblems.Sum(p => p.ImpactValue ?? 0m);
        var impact = new ReportImpact(
            totalLoss,
            revenue,
            revenue is > 0 ? decimal.Round(totalLoss / revenue.Value * 100m, 2) : null,
            [.. yearProblems.GroupBy(p => p.ImpactCategory)
                .Select(g => new ReportImpactCategory(g.Key, CategoryNames[g.Key], g.Sum(p => p.ImpactValue ?? 0m), g.Count()))
                .OrderByDescending(c => c.EstimatedLoss)]);

        return new ExecutiveReportModel(
            company.Name,
            company.Industry,
            company.Currency,
            current.Year,
            previous?.Year,
            timeProvider.GetUtcNow().UtcDateTime,
            ToScore(allScores.GetValueOrDefault(current.Year), allScores.GetValueOrDefault(current.Year - 1)),
            impact,
            [.. KpiCatalog.All.Select(d => new ReportKpi(d.Code, d.Name, d.Formula, d.Unit, d.HigherIsBetter, current.Get(d.Code), previous?.Get(d.Code)))],
            [.. financial.OrderBy(f => f.Year).Select(f => new ReportTrendPoint(
                f.Year, f.Revenue, f.TotalCost, kpis.FirstOrDefault(k => k.Year == f.Year)?.EbitdaMargin))],
            ordered,
            await WarningsAsync(companyId, current.Year, cancellationToken),
            SummaryText(await summaries.GetByCompanyAndYearAsync(companyId, current.Year, cancellationToken)));
    }

    /// <summary>Resumen ejecutivo con IA como texto del informe (párrafos y prioridades).</summary>
    private static string? SummaryText(ExecutiveSummary? summary) => summary is null || summary.Paragraphs.Count == 0
        ? null
        : string.Join("\n\n", summary.Paragraphs)
            + (summary.Priorities.Count == 0
                ? string.Empty
                : "\n\nPrioridades: " + string.Join(" · ", summary.Priorities.Select((p, i) => $"{i + 1}. {p}")))
            + $"\n\n(Generado con IA, modelo {summary.Model}, el {summary.GeneratedAt:dd/MM/yyyy}.)";

    /// <summary>Recomendación de IA como texto (diagnóstico y acciones), o <c>null</c>.</summary>
    private static string? AiText(string? json) => Insights.AiEnrichmentService.ParseRecommendation(json) is { } ai
        ? ai.Summary + " Acciones: " + string.Join(" ", ai.Actions.Take(3).Select((a, i) => $"{i + 1}) {a.Title}."))
        : null;

    private async Task<IReadOnlyList<ReportWarning>> WarningsAsync(Guid companyId, int year, CancellationToken cancellationToken)
    {
        // Advertencias de calidad de la carga más reciente que incluye el año.
        var list = await uploads.ListAsync(u => u.CompanyId == companyId && u.Status == UploadStatus.Processed, cancellationToken);
        var latest = list.Where(u => u.Years.Contains(year)).MaxBy(u => u.CreatedAt);
        return latest is null
            ? []
            : [.. latest.Warnings.Select(w => new ReportWarning(w.Message, Location(w)))];
    }

    private static string? Location(UploadIssue issue)
    {
        var parts = new List<string>();
        if (issue.Sheet is not null)
        {
            parts.Add($"Hoja {issue.Sheet}");
        }

        if (issue.Row is not null)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"fila {issue.Row}"));
        }

        if (issue.Column is not null)
        {
            parts.Add($"columna {issue.Column}");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static ReportScore? ToScore(Score? score, Score? previous) => score is null
        ? null
        : new ReportScore(
            score.TotalScore,
            score.Interpretation,
            ScoreEngine.Label(score.Interpretation),
            previous is null ? null : score.TotalScore - previous.TotalScore,
            score.Components.Any(c => c.Value is null),
            [.. score.Components.Select(c => new ReportScoreComponent(ScoreService.NameOf(c.Dimension), c.Value, c.Weight, c.BaseWeight, c.MissingReason))]);

    /// <summary>Nombre de archivo seguro: minúsculas, sin acentos ni símbolos.</summary>
    public static string Slug(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');
        }

        var slug = string.Join('_', builder.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries));
        return slug.Length == 0 ? "empresa" : slug[..Math.Min(slug.Length, 60)];
    }
}
