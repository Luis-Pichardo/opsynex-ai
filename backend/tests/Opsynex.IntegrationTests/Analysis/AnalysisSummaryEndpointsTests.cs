using System.Net;
using Opsynex.Application.Analysis;
using Opsynex.Application.Homologation;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;
using Opsynex.IntegrationTests.Uploads;

namespace Opsynex.IntegrationTests.Analysis;

/// <summary>Endpoints que usa el asistente de carga del frontend (etapa 09).</summary>
public sealed class AnalysisSummaryEndpointsTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Summary_DescribesTheLatestAnalyzedYear()
    {
        var companyId = await TestUsers.CreateCompanyAsync(factory.Database);
        var (analyst, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst, companyId);
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Client, companyId);
        using (analyst)
        using (client)
        {
            var empty = await (await client.GetAsync("/api/v1/analysis/summary", Ct)).ReadAsync<AnalysisSummaryDto>();
            empty.LatestYear.ShouldBeNull();
            empty.Years.ShouldBeEmpty();
            empty.Problems.Total.ShouldBe(0);

            (await analyst.UploadSampleAsync("libro_sintetico_3a.xls")).StatusCode.ShouldBe(HttpStatusCode.Created);

            var summary = await (await client.GetAsync("/api/v1/analysis/summary", Ct)).ReadAsync<AnalysisSummaryDto>();
            var score = await (await client.GetAsync("/api/v1/score?year=2024", Ct)).ReadAsync<ScoreResponse>();
            var problems = await (await client.GetAsync("/api/v1/problems?year=2024", Ct)).ReadAsync<ProblemPage>();
            var impact = await (await client.GetAsync("/api/v1/impact/summary", Ct)).ReadAsync<ImpactSummaryDto>();

            summary.Years.ShouldBe([2022, 2023, 2024]);
            summary.LatestYear.ShouldBe(2024);
            summary.KpisTotal.ShouldBe(14);
            summary.KpisCalculated.ShouldBe(13); // el costo por unidad necesita unidades producidas
            summary.Problems.Total.ShouldBe(problems.TotalCount);
            summary.Problems.Low.ShouldBe(problems.Items.Count(p => p.Severity == Severity.Low));
            (summary.Problems.High + summary.Problems.Medium + summary.Problems.Low).ShouldBe(summary.Problems.Total);
            summary.Score.ShouldBe(score.Years.Single().TotalScore);
            summary.Interpretation.ShouldBe(ScoreInterpretation.Healthy);
            summary.InterpretationLabel.ShouldBe("Operación saludable");
            summary.EstimatedLoss.ShouldBe(impact.TotalEstimatedLoss);
            summary.CalculatedAt.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task Summary_AdminMustChooseCompany()
    {
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            await (await admin.GetAsync("/api/v1/analysis/summary", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "analysis.company_required");
        }
    }

    [Fact]
    public async Task Categories_ListTheCatalogInSpanish_ForAnalysts()
    {
        var companyId = await TestUsers.CreateCompanyAsync(factory.Database);
        var (analyst, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst, companyId);
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Client, companyId);
        using (analyst)
        using (client)
        {
            var categories = await (await analyst.GetAsync("/api/v1/mappings/categories", Ct)).ReadAsync<List<CategoryDto>>();
            categories.Count.ShouldBe(Enum.GetValues<OpsynexCategory>().Length);
            categories.Single(c => c.Category == OpsynexCategory.Revenue).Name.ShouldBe("Ingresos");

            (await client.GetAsync("/api/v1/mappings/categories", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task UploadList_IncludesWhoUploaded()
    {
        var companyId = await TestUsers.CreateCompanyAsync(factory.Database);
        var (analyst, user) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst, companyId);
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Client, companyId);
        using (analyst)
        using (client)
        {
            (await analyst.UploadSampleAsync("libro_sintetico_2a.xls")).StatusCode.ShouldBe(HttpStatusCode.Created);

            var list = await (await client.GetAsync("/api/v1/uploads", Ct)).ReadAsync<List<UploadListItemDto>>();

            list.Single().UploadedBy.ShouldBe($"{user.Role} de prueba");
        }
    }
}
