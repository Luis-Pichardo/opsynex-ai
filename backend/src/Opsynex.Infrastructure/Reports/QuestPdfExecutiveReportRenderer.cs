using Opsynex.Application.Reports;
using Opsynex.Domain.Enums;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Opsynex.Infrastructure.Reports;

/// <summary>
/// Informe ejecutivo en PDF con QuestPDF (licencia Community: ver la documentación de despliegue).
/// Tamaño Carta, fuente Inter embebida, colores de la guía de diseño y pie con la empresa y el
/// número de página.
/// </summary>
internal sealed class QuestPdfExecutiveReportRenderer : IExecutiveReportRenderer
{
    private const string Font = "Inter";
    private static readonly Lazy<bool> Initialized = new(Initialize);

    public byte[] Render(ExecutiveReportModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _ = Initialized.Value;
        return Document.Create(document => document.Page(page => ComposePage(page, model)))
            .WithMetadata(new DocumentMetadata
            {
                Title = $"Informe ejecutivo {model.Year} — {model.CompanyName}",
                Author = "Opsynex AI",
                Subject = "Diagnóstico financiero-operacional",
                Creator = "Opsynex AI",
                CreationDate = new DateTimeOffset(model.GeneratedAt, TimeSpan.Zero),
            })
            .GeneratePdf();
    }

    private static bool Initialize()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var assembly = typeof(QuestPdfExecutiveReportRenderer).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"No se encontró la fuente embebida {name}.");
            FontManager.RegisterFontFromStream(stream);
        }

        return true;
    }

    private static void ComposePage(PageDescriptor page, ExecutiveReportModel model)
    {
        page.Size(PageSizes.Letter);
        page.Margin(40);
        page.PageColor(ReportTheme.Surface);
        page.DefaultTextStyle(style => style.FontFamily(Font).FontSize(10).FontColor(ReportTheme.Foreground).LineHeight(1.35f));

        page.Footer().PaddingTop(8).BorderTop(1).BorderColor(ReportTheme.Border).Row(row =>
        {
            row.RelativeItem().Text($"{model.CompanyName} · Informe ejecutivo {model.Year}").FontSize(8).FontColor(ReportTheme.ForegroundSecondary);
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.DefaultTextStyle(s => s.FontSize(8).FontColor(ReportTheme.ForegroundSecondary));
                text.Span("Página ");
                text.CurrentPageNumber();
                text.Span(" de ");
                text.TotalPages();
            });
        });

        page.Content().Column(column =>
        {
            column.Spacing(18);
            column.Item().Element(c => Cover(c, model));
            if (model.ExecutiveSummary is { Length: > 0 } summary)
            {
                column.Item().Element(c => Section(c, "Resumen ejecutivo (generado con IA)", body => body.Text(summary)));
            }

            column.Item().Element(c => ScoreSection(c, model));
            column.Item().Element(c => ImpactSection(c, model));
            column.Item().Element(c => KpiSection(c, model));
            column.Item().Element(c => TrendSection(c, model));
            column.Item().Element(c => ProblemsSection(c, model));
            column.Item().Element(c => WarningsSection(c, model));
            column.Item().Element(Methodology);
        });
    }

    private static void Cover(IContainer container, ExecutiveReportModel model) =>
        container.Background(ReportTheme.Sidebar).Padding(24).Column(column =>
        {
            column.Spacing(4);
            column.Item().Text("Opsynex AI").FontSize(11).SemiBold().FontColor(ReportTheme.SidebarMuted);
            column.Item().PaddingTop(8).Text("Informe ejecutivo").FontSize(26).Bold().FontColor(ReportTheme.SidebarForeground);
            column.Item().Text(model.CompanyName).FontSize(16).SemiBold().FontColor(ReportTheme.SidebarForeground);
            column.Item().PaddingTop(6).Text(text =>
            {
                text.DefaultTextStyle(s => s.FontSize(10).FontColor(ReportTheme.SidebarMuted));
                text.Span($"Año analizado: {model.Year}");
                text.Span($"   ·   Industria: {(model.Industry == Industry.Manufacturing ? "Manufactura" : "Servicios")}");
                text.Span($"   ·   Moneda: {model.Currency}");
                text.Span($"   ·   Generado el {ReportFormat.Date(model.GeneratedAt)}");
            });
        });

    private static void Section(IContainer container, string title, Action<IContainer> body) =>
        // EnsureSpace: el título no queda solo al pie de una página.
        container.EnsureSpace(140).Column(column =>
        {
            column.Spacing(8);
            column.Item().Text(title).FontSize(15).SemiBold();
            column.Item().Element(body);
        });

    private static void ScoreSection(IContainer container, ExecutiveReportModel model) =>
        Section(container, "Score Opsynex", body =>
        {
            if (model.Score is not { } score)
            {
                body.Text("No hay datos suficientes para calcular el score de este año.").FontColor(ReportTheme.ForegroundSecondary);
                return;
            }

            var (text, soft) = ScoreColors(score.Interpretation);
            body.Row(row =>
            {
                row.ConstantItem(150).Border(1).BorderColor(ReportTheme.Border).Padding(14).Column(c =>
                {
                    c.Item().Text(ReportFormat.Number(score.Total)).FontSize(40).Bold();
                    c.Item().Text("de 100").FontSize(9).FontColor(ReportTheme.ForegroundSecondary);
                    c.Item().PaddingTop(6).Background(soft).PaddingHorizontal(6).PaddingVertical(2)
                        .Text(score.InterpretationLabel).FontSize(9).SemiBold().FontColor(text);
                    if (score.ChangeFromPrevious is { } change)
                    {
                        c.Item().PaddingTop(6).Text($"{ReportFormat.Signed(change, "0.0")} pts vs. {model.Year - 1}")
                            .FontSize(9).FontColor(change >= 0 ? ReportTheme.GoodText : ReportTheme.CriticalText);
                    }
                });
                row.ConstantItem(16);
                row.RelativeItem().Column(c =>
                {
                    c.Spacing(6);
                    foreach (var component in score.Components)
                    {
                        c.Item().Row(r =>
                        {
                            r.ConstantItem(130).Text(component.Name).FontSize(9);
                            r.RelativeItem().PaddingTop(3).Height(8).Row(bar =>
                            {
                                var value = (float)Math.Clamp(component.Value ?? 0m, 0m, 100m);
                                if (value > 0)
                                {
                                    bar.RelativeItem(value).Background(ReportTheme.Chart1);
                                }

                                if (value < 100)
                                {
                                    bar.RelativeItem(100 - value).Background(ReportTheme.SurfaceMuted);
                                }
                            });
                            r.ConstantItem(36).AlignRight().Text(ReportFormat.Number(component.Value)).FontSize(9).SemiBold();
                            r.ConstantItem(60).AlignRight().Text($"peso {ReportFormat.Number(component.Weight * 100m)} %")
                                .FontSize(8).FontColor(ReportTheme.ForegroundSecondary);
                        });
                        if (component.MissingReason is not null)
                        {
                            c.Item().Text($"Sin datos: {component.MissingReason} Su peso se repartió entre los demás componentes.")
                                .FontSize(8).FontColor(ReportTheme.ForegroundSecondary);
                        }
                    }
                });
            });
        });

    private static void ImpactSection(IContainer container, ExecutiveReportModel model) =>
        Section(container, "Impacto financiero estimado", body => body.Column(c =>
        {
            c.Spacing(6);
            c.Item().Text(text =>
            {
                text.Span(ReportFormat.Money(model.Impact.TotalEstimatedLoss, model.Currency)).FontSize(20).Bold();
                if (model.Impact.LossPercentOfRevenue is { } pct)
                {
                    text.Span($"   equivale al {ReportFormat.Number(pct, 2)} % de los ingresos de {model.Year}")
                        .FontSize(10).FontColor(ReportTheme.ForegroundSecondary);
                }
            });
            if (model.Impact.ByCategory.Count == 0)
            {
                c.Item().Text("No se detectaron problemas con pérdida estimada.").FontColor(ReportTheme.ForegroundSecondary);
                return;
            }

            c.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(2);
                });
                HeaderRow(table, "Categoría", "Problemas", "Pérdida estimada");
                foreach (var category in model.Impact.ByCategory)
                {
                    Cell(table).Text(category.Name);
                    Cell(table).AlignRight().Text(ReportFormat.Number(category.Problems));
                    Cell(table).AlignRight().Text(category.Category == ImpactCategory.FinancialAlert
                        ? "No se cuantifica"
                        : ReportFormat.Money(category.EstimatedLoss, model.Currency));
                }
            });
            c.Item().Text("Las estimaciones de distintos problemas pueden medir parte del mismo efecto: el total es un orden de magnitud para priorizar, no una cifra contable.")
                .FontSize(8).FontColor(ReportTheme.ForegroundSecondary);
        }));

    private static void KpiSection(IContainer container, ExecutiveReportModel model) =>
        Section(container, "Indicadores clave (KPIs)", body => body.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(3);
                cols.RelativeColumn(2);
                cols.RelativeColumn(2);
                cols.RelativeColumn(2);
            });
            HeaderRow(table, "Indicador", model.Year.ToString(ReportFormat.Es), model.PreviousYear?.ToString(ReportFormat.Es) ?? "Año anterior", "Variación");
            foreach (var kpi in model.Kpis.Where(k => k.Value is not null || k.Previous is not null))
            {
                Cell(table).Column(c =>
                {
                    c.Item().Text(kpi.Name);
                    c.Item().Text(kpi.Formula).FontSize(7).FontColor(ReportTheme.ForegroundMuted);
                });
                Cell(table).AlignRight().Text(ReportFormat.Kpi(kpi.Value, kpi.Unit, model.Currency)).SemiBold();
                Cell(table).AlignRight().Text(ReportFormat.Kpi(kpi.Previous, kpi.Unit, model.Currency)).FontColor(ReportTheme.ForegroundSecondary);
                var (change, favorable) = ReportFormat.Change(kpi.Value, kpi.Previous, kpi.Unit, kpi.HigherIsBetter);
                var color = favorable switch
                {
                    true => ReportTheme.GoodText,
                    false => ReportTheme.CriticalText,
                    null => ReportTheme.ForegroundSecondary,
                };
                var meaning = favorable switch { true => " (favorable)", false => " (desfavorable)", null => string.Empty };
                Cell(table).AlignRight().Text($"{change}{meaning}").FontSize(9).FontColor(color);
            }
        }));

    private static void TrendSection(IContainer container, ExecutiveReportModel model) =>
        Section(container, "Tendencia: ingresos y costo total", body =>
        {
            var svg = TrendChartSvg.Render(model.Trend, model.Currency);
            if (svg is null)
            {
                body.Text("No hay datos de ingresos y costos para graficar.").FontColor(ReportTheme.ForegroundSecondary);
                return;
            }

            body.Column(c =>
            {
                c.Item().Text($"Montos anuales en {model.Currency}, {model.Trend[0].Year}–{model.Trend[^1].Year}.")
                    .FontSize(8).FontColor(ReportTheme.ForegroundSecondary);
                c.Item().Height(TrendChartSvg.Height).Svg(svg);
            });
        });

    private static void ProblemsSection(IContainer container, ExecutiveReportModel model) =>
        Section(container, "Problemas detectados y recomendaciones", body => body.Column(c =>
        {
            c.Spacing(10);
            if (model.Problems.Count == 0)
            {
                c.Item().Text("No se detectaron problemas en este año.").FontColor(ReportTheme.ForegroundSecondary);
                return;
            }

            foreach (var problem in model.Problems)
            {
                var (label, text, soft) = SeverityStyle(problem.Severity);
                c.Item().ShowEntire().Border(1).BorderColor(ReportTheme.Border).Padding(10).Column(block =>
                {
                    block.Spacing(4);
                    block.Item().Row(row =>
                    {
                        row.AutoItem().Background(soft).PaddingHorizontal(6).PaddingVertical(2)
                            .Text($"Severidad {label}").FontSize(8).SemiBold().FontColor(text);
                        row.ConstantItem(8);
                        row.RelativeItem().Text($"{problem.Priority}. {problem.Title}").FontSize(11).SemiBold();
                        row.AutoItem().AlignRight().Text(problem.ImpactValue is { } loss
                            ? ReportFormat.Money(loss, model.Currency)
                            : "Sin pérdida cuantificada").FontSize(10).SemiBold();
                    });
                    block.Item().Text(problem.Description).FontColor(ReportTheme.ForegroundSecondary);
                    block.Item().Text(t =>
                    {
                        t.Span("Recomendación: ").SemiBold();
                        t.Span(problem.Recommendation);
                    });
                    if (problem.AiRecommendation is { } ai)
                    {
                        block.Item().Background(ReportTheme.PrimarySoft).Padding(6).Text(t =>
                        {
                            t.Span("Recomendación con IA: ").SemiBold().FontColor(ReportTheme.InfoText);
                            t.Span(ai);
                        });
                    }
                });
            }
        }));

    private static void WarningsSection(IContainer container, ExecutiveReportModel model) =>
        Section(container, "Calidad de los datos", body => body.Column(c =>
        {
            c.Spacing(4);
            if (model.Warnings.Count == 0)
            {
                c.Item().Text("La carga no tuvo advertencias de calidad.").FontColor(ReportTheme.ForegroundSecondary);
                return;
            }

            c.Item().Text("Advertencias de la carga (no impidieron el análisis, conviene revisarlas con el contador):")
                .FontColor(ReportTheme.ForegroundSecondary);
            foreach (var warning in model.Warnings.Take(20))
            {
                c.Item().Row(row =>
                {
                    row.ConstantItem(10).Text("•");
                    row.RelativeItem().Text(t =>
                    {
                        t.Span(warning.Message);
                        if (warning.Location is not null)
                        {
                            t.Span($" ({warning.Location})").FontColor(ReportTheme.ForegroundSecondary);
                        }
                    });
                });
            }

            if (model.Warnings.Count > 20)
            {
                c.Item().Text($"… y {model.Warnings.Count - 20} advertencias más en la plataforma.").FontColor(ReportTheme.ForegroundSecondary);
            }
        }));

    private static void Methodology(IContainer container) =>
        Section(container, "Nota metodológica", body => body.Text(
            "Opsynex AI homologa las cuentas del libro contable a categorías estándar, calcula 14 KPIs por año, " +
            "detecta ineficiencias con 13 reglas configurables y estima su impacto económico según su categoría " +
            "(pérdida de margen, exceso de costos, pérdida de productividad y desperdicio operacional). El score de " +
            "0 a 100 pondera rentabilidad (30 %), eficiencia de costos (25 %), productividad (25 %) y estabilidad " +
            "financiera (20 %), con KPIs normalizados según referencias de la industria. Las cifras son estimaciones " +
            "para priorizar decisiones y no sustituyen la auditoría de los estados financieros.")
            .FontSize(9).FontColor(ReportTheme.ForegroundSecondary));

    private static void HeaderRow(TableDescriptor table, params string[] titles)
    {
        for (var i = 0; i < titles.Length; i++)
        {
            var cell = table.Cell().Background(ReportTheme.SurfaceMuted).PaddingVertical(5).PaddingHorizontal(6);
            (i == 0 ? cell : cell.AlignRight()).Text(titles[i].ToUpper(ReportFormat.Es)).FontSize(7).SemiBold()
                .LetterSpacing(0.06f).FontColor(ReportTheme.ForegroundSecondary);
        }
    }

    private static IContainer Cell(TableDescriptor table) =>
        table.Cell().BorderBottom(1).BorderColor(ReportTheme.Border).PaddingVertical(5).PaddingHorizontal(6);

    private static (string Text, string Soft) ScoreColors(ScoreInterpretation interpretation) => interpretation switch
    {
        ScoreInterpretation.Healthy => (ReportTheme.GoodText, ReportTheme.GoodSoft),
        ScoreInterpretation.Improvable => (ReportTheme.WarningText, ReportTheme.WarningSoft),
        _ => (ReportTheme.CriticalText, ReportTheme.CriticalSoft),
    };

    private static (string Label, string Text, string Soft) SeverityStyle(Severity severity) => severity switch
    {
        Severity.High => ("alta", ReportTheme.CriticalText, ReportTheme.CriticalSoft),
        Severity.Medium => ("media", ReportTheme.SeriousText, ReportTheme.SeriousSoft),
        _ => ("baja", ReportTheme.WarningText, ReportTheme.WarningSoft),
    };
}
