using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Opsynex.Api.Demo;
using Opsynex.Application.Analysis;
using Opsynex.Application.Industrial;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;
using Opsynex.IntegrationTests.Uploads;

namespace Opsynex.IntegrationTests.Api;

/// <summary>
/// <c>seed-demo</c> deja la plataforma lista para presentar en menos de 1 minuto: empresas
/// ficticias con datos sintéticos, usuarios de cada rol y el análisis ejecutado (etapa 13).
/// </summary>
public sealed class DemoSeedTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SeedDemo_CreatesCompaniesUsersAndAnalysis_InUnderOneMinute()
    {
        // Los libros sintéticos están en sample-data junto a las pruebas (la ruta por defecto).
        Directory.Exists(UploadClient.SampleDirectory).ShouldBeTrue();
        var watch = Stopwatch.StartNew();
        var result = await factory.Services.SeedDemoAsync(Ct);
        watch.Elapsed.ShouldBeLessThan(TimeSpan.FromMinutes(1));

        result.Users.Select(u => u.Role).ShouldBe([UserRole.Admin, UserRole.Analyst, UserRole.Client, UserRole.Analyst, UserRole.Client]);

        // El cliente de la manufacturera entra con la contraseña demo y ve el análisis completo.
        using var client = factory.CreateClient();
        var token = await TestUsers.LoginAsync(client, "cliente.manufactura@demo.opsynex.local", DemoSeeder.DefaultPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var summary = await (await client.GetAsync("/api/v1/analysis/summary", Ct)).ReadAsync<AnalysisSummaryDto>();
        summary.Years.ShouldBe([2022, 2023, 2024]);
        summary.Score.ShouldNotBeNull();
        var industrial = await (await client.GetAsync("/api/v1/industrial/kpis", Ct)).ReadAsync<IndustrialKpisDto>();
        industrial.HasData.ShouldBeTrue();
        industrial.Machines.Count.ShouldBe(3);
        var problems = await (await client.GetAsync("/api/v1/problems?year=2024&pageSize=100", Ct)).ReadAsync<ProblemPage>();
        problems.Items.ShouldContain(p => p.Code == "LOW_OEE");

        // La empresa de servicios tiene su propio análisis (sin datos industriales).
        using var services = factory.CreateClient();
        services.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestUsers.LoginAsync(services, "analista.servicios@demo.opsynex.local", DemoSeeder.DefaultPassword));
        (await (await services.GetAsync("/api/v1/analysis/summary", Ct)).ReadAsync<AnalysisSummaryDto>()).Years.ShouldBe([2023, 2024]);
        (await (await services.GetAsync("/api/v1/industrial/kpis", Ct)).ReadAsync<IndustrialKpisDto>()).HasData.ShouldBeFalse();

        // Es idempotente: una segunda ejecución no duplica datos.
        var again = await factory.Services.SeedDemoAsync(Ct);
        again.Steps.ShouldAllBe(s => s.Contains("ya existía", StringComparison.Ordinal));
        (await (await client.GetAsync("/api/v1/problems?year=2024&pageSize=100", Ct)).ReadAsync<ProblemPage>()).TotalCount.ShouldBe(problems.TotalCount);
    }
}
