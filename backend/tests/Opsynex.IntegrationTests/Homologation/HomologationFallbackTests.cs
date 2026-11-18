using System.Net;
using Microsoft.EntityFrameworkCore;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;
using Opsynex.IntegrationTests.Uploads;

namespace Opsynex.IntegrationTests.Homologation;

/// <summary>Con el servicio de IA apagado la homologación sigue funcionando (fallback local).</summary>
public sealed class HomologationFallbackTests(UnreachableAiApiFactory factory) : IClassFixture<UnreachableAiApiFactory>
{
    private const string Book = "libro_sintetico_2a.xls";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WithAiServiceDown_UploadIsStillProcessedWithFallback()
    {
        var (client, analyst) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            var started = DateTime.UtcNow;
            var report = await (await client.UploadSampleAsync(Book)).ReadAsync<UploadReportDto>(HttpStatusCode.Created);
            (DateTime.UtcNow - started).ShouldBeLessThan(TimeSpan.FromSeconds(30));

            report.Status.ShouldBe(UploadStatus.Processed);
            report.AnalysisStatus.ShouldBe(AnalysisStatus.Completed);

            await using var context = factory.Database.CreateContext();
            (await context.AccountMappings.CountAsync(m => m.CompanyId == analyst.CompanyId && m.Method == MappingMethod.Fallback, Ct))
                .ShouldBeGreaterThan(20);
            var data = await context.FinancialData.SingleAsync(f => f.CompanyId == analyst.CompanyId && f.Year == 2024, Ct);
            data.Revenue.ShouldBe(Expected.V(Book, 2024, "net_sales"));
            data.Cogs.ShouldBe(Expected.V(Book, 2024, "cogs"));
            data.NetIncome.ShouldBe(Expected.V(Book, 2024, "net_income"));
            data.Ebitda.ShouldBe(Expected.V(Book, 2024, "ebitda"));
            data.TotalAssets.ShouldBe(Expected.V(Book, 2024, "total_assets"));
            data.TotalLiabilities.ShouldBe(Expected.V(Book, 2024, "total_liabilities"));
            data.EnergyCost.ShouldBe(Expected.Sum(Book, 2024, "g_energia", "fab_energia"));
        }
    }

    [Fact]
    public async Task Health_IsDegradedNotUnhealthy()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.ShouldContain("\"name\":\"ai-service\",\"status\":\"Degraded\"");
    }
}
