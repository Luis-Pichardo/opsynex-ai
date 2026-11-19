using System.Net;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;
using Opsynex.IntegrationTests.Uploads;
using UglyToad.PdfPig;

namespace Opsynex.IntegrationTests.Analysis;

/// <summary>Informe ejecutivo en PDF (GET /api/v1/reports/executive).</summary>
public sealed class ReportsEndpointsTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Text(byte[] pdf, out int pages)
    {
        using var document = PdfDocument.Open(pdf);
        pages = document.NumberOfPages;
        // Se unen las palabras con espacios para buscar frases completas.
        return string.Join(' ', document.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
    }

    [Theory]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.Analyst)]
    public async Task ExecutiveReport_HasEveryRequiredSection(UserRole role)
    {
        var companyId = await TestUsers.CreateCompanyAsync(factory.Database, "Industrias Sintéticas S.R.L.");
        var (analyst, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst, companyId);
        var (reader, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, role, companyId);
        using (analyst)
        using (reader)
        {
            (await analyst.UploadSampleAsync("libro_sintetico_3a.xls")).StatusCode.ShouldBe(HttpStatusCode.Created);

            var response = await reader.GetAsync("/api/v1/reports/executive", Ct);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
            response.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("informe_industrias_sinteticas_s_r_l_2024.pdf");
            var pdf = await response.Content.ReadAsByteArrayAsync(Ct);
            var text = Text(pdf, out var pages);

            pages.ShouldBeGreaterThan(1);
            text.ShouldContain("Informe ejecutivo");
            text.ShouldContain("Industrias Sintéticas S.R.L.");
            text.ShouldContain("Año analizado: 2024");
            text.ShouldContain("Score Opsynex");
            text.ShouldContain("83"); // score 2024 del libro sintético (83,24)
            text.ShouldContain("Operación saludable");
            text.ShouldContain("Impacto financiero estimado");
            text.ShouldContain("Indicadores clave"); // PdfPig no extrae algunos signos (paréntesis, «−»)
            text.ShouldContain("Margen EBITDA");
            text.ShouldContain("Tendencia: ingresos y costo total");
            text.ShouldContain("Problemas detectados y recomendaciones");
            text.ShouldContain("Deterioro de rentabilidad");
            text.ShouldContain("Ineficiencia administrativa");
            text.ShouldContain("Ineficiencia en costos de personal");
            text.ShouldContain("Recomendación:");
            text.ShouldContain("Calidad de los datos");
            text.ShouldContain("Nota metodológica");
            text.ShouldContain("Página 1 de");
        }
    }

    [Fact]
    public async Task ExecutiveReport_ForAPreviousYear_AndErrors()
    {
        var companyId = await TestUsers.CreateCompanyAsync(factory.Database);
        var (analyst, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst, companyId);
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (analyst)
        using (admin)
        {
            await (await analyst.GetAsync("/api/v1/reports/executive", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "reports.no_data");
            await (await admin.GetAsync("/api/v1/reports/executive", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "reports.company_required");

            (await analyst.UploadSampleAsync("libro_sintetico_3a.xls")).StatusCode.ShouldBe(HttpStatusCode.Created);

            var response = await analyst.GetAsync("/api/v1/reports/executive?year=2023", Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var text = Text(await response.Content.ReadAsByteArrayAsync(Ct), out _);
            text.ShouldContain("Año analizado: 2023");
            text.ShouldContain("Problema de absorción de costos");

            await (await analyst.GetAsync("/api/v1/reports/executive?year=2019", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "reports.no_data");
            (await admin.GetAsync($"/api/v1/reports/executive?companyId={companyId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

            using var anonymous = factory.CreateClient();
            (await anonymous.GetAsync("/api/v1/reports/executive", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }
}
